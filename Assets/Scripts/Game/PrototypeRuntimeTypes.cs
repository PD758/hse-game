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
    Flashlight,
    Story,
    Heal,
}

internal enum EnemyMode
{
    Patrol,
    Investigate,
    Hunt,
}

internal enum EnemyArchetype
{
    Patrol,
    Hunter,
    Brute,
    Caller,
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
    Flashlight,
    Story,
    Heal,
    Player,
    Stone,
    Enemy,
}

internal enum AbilitySlot
{
    None,
    Remote,
    Flashlight,
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
    public string AlertGroup;
    public EnemyArchetype Archetype = EnemyArchetype.Patrol;
    public int Level = 3;
    public int Hp = 2;
    public float HearingOverride;
    public float VisionOverride;
    public float StunTimer;
    public float HitFlashTimer;
    public float AttackWindupTimer;
    public float AttackStrikeTimer;
    public float AttackRecoveryTimer;
    public float LostSightTimer;
    public float SearchTimer;
    public float AlertTimer;
    public float FlankCooldown;
    public float CallHelpCooldown;
    public bool AttackApplied;
    public readonly List<Vector2Int> SearchPoints = new List<Vector2Int>();
    public int SearchIndex;
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
