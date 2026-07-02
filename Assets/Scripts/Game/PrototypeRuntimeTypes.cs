using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

internal enum Tile
{
    Floor,
    Wall,
    Exit,
    Plate,
    Gate,
    Rubble,
    Trap,
    Remote,
    Story,
    Heal,
}

internal enum EnemyMode
{
    Patrol,
    Investigate,
    Hunt,
}

internal enum FacingDirection
{
    Down,
    Up,
    Left,
    Right,
}

internal enum SpriteMark
{
    None,
    Plate,
    Gate,
    Exit,
    Rubble,
    Trap,
    Remote,
    Story,
    Heal,
    Player,
    Stone,
    Enemy,
}

internal sealed class Stone
{
    public Vector2Int Cell;
    public Vector3 Target;
    public bool Moving;
    public GameObject View;
}

internal sealed class Enemy
{
    public string Id;
    public Vector2 Position;
    public readonly List<Vector2Int> Patrol = new List<Vector2Int>();
    public int PatrolIndex;
    public EnemyMode Mode;
    public Vector2Int LastSeen;
    public BranchChoice Branch;
    public string Group;
    public int Level = 3;
    public int Hp = 2;
    public float StunTimer;
    public float HitFlashTimer;
    public float AttackWindupTimer;
    public float AttackStrikeTimer;
    public float AttackRecoveryTimer;
    public float LostSightTimer;
    public bool AttackApplied;
    public Vector2 AttackDirection = Vector2.down;
    public Vector2 LookDirection = Vector2.down;
    public Vector2 KnockbackVelocity;
    public GameObject View;
    public GameObject TelegraphView;
    public Light2D Light;
    public SpriteRenderer BeamRenderer;
}

internal sealed class CombatEffect
{
    public GameObject View;
    public SpriteRenderer Renderer;
    public Vector2 Velocity;
    public Vector3 StartScale;
    public Vector3 EndScale;
    public Color Color;
    public Light2D Light;
    public float LightStartIntensity;
    public float Duration;
    public float Age;
    public float RotationSpeed;
}
