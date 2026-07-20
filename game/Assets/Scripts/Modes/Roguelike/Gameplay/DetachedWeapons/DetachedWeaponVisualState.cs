using UnityEngine;

namespace Game.Modes.Roguelike.Gameplay.DetachedWeapons
{
  [DisallowMultipleComponent]
  public sealed class DetachedWeaponVisualState : MonoBehaviour
  {
    public const int MaxBeams = 12;

    public string VisualId { get; private set; }
    public bool Warning { get; private set; }
    public float BeamWidth { get; private set; }
    public int BeamCount { get; private set; }
    public bool AttackActive { get; private set; }
    public bool IntroActive { get; private set; }
    public readonly Vector3[] BeamStarts = new Vector3[MaxBeams];
    public readonly Vector3[] BeamEnds = new Vector3[MaxBeams];

    public void SetVisual(string visualId) => VisualId = visualId;
    public void SetAttackActive(bool active) => AttackActive = active;
    public void SetIntroActive(bool active) => IntroActive = active;

    public void ResetPresentationState()
    {
      ClearBeams();
      AttackActive = false;
      Warning = false;
      IntroActive = false;
    }

    public void ClearBeams()
    {
      Warning = false;
      BeamCount = 0;
    }

    public void BeginFrame(bool warning, float width)
    {
      Warning = warning;
      BeamWidth = width;
      BeamCount = 0;
    }

    public void AddBeam(Vector3 start, Vector3 end)
    {
      if (BeamCount >= MaxBeams)
        return;
      BeamStarts[BeamCount] = start;
      BeamEnds[BeamCount] = end;
      BeamCount++;
    }
  }
}
