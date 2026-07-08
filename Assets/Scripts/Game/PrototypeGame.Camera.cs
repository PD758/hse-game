using Cinemachine;
using UnityEngine;

public sealed partial class PrototypeGame
{
    private const string GameplayVirtualCameraName = "Gameplay Virtual Camera";
    private const string GameplayCameraBoundsName = "Gameplay Camera Bounds";
    private const float GameplayCameraBoundsPadding = 2.0f;

    private CinemachineVirtualCamera gameplayVirtualCamera;
    private CinemachineConfiner2D gameplayCameraConfiner;
    private PolygonCollider2D gameplayCameraBounds;

    private void EnsureGameplayCameraRig()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        camera.orthographic = true;
        camera.orthographicSize = GameplayCameraSize;
        camera.backgroundColor = GameplayCameraBackground;

        if (!camera.TryGetComponent(out CinemachineBrain brain))
            brain = camera.gameObject.AddComponent<CinemachineBrain>();
        brain.m_DefaultBlend.m_Time = 0.16f;

        GameObject virtualCameraObject = FindSceneObjectIncludingInactive(GameplayVirtualCameraName);
        if (virtualCameraObject == null)
        {
            virtualCameraObject = new GameObject(GameplayVirtualCameraName);
            virtualCameraObject.transform.SetParent(transform);
        }

        gameplayVirtualCamera = virtualCameraObject.GetComponent<CinemachineVirtualCamera>();
        if (gameplayVirtualCamera == null)
            gameplayVirtualCamera = virtualCameraObject.AddComponent<CinemachineVirtualCamera>();
        gameplayVirtualCamera.Priority = 20;
        gameplayVirtualCamera.m_Lens.OrthographicSize = GameplayCameraSize;
        gameplayVirtualCamera.m_Lens.NearClipPlane = camera.nearClipPlane;
        gameplayVirtualCamera.m_Lens.FarClipPlane = camera.farClipPlane;

        CinemachineFramingTransposer framing = gameplayVirtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (framing == null)
            framing = gameplayVirtualCamera.AddCinemachineComponent<CinemachineFramingTransposer>();
        framing.m_XDamping = 1.15f;
        framing.m_YDamping = 1.28f;
        framing.m_ZDamping = 0f;
        framing.m_ScreenX = 0.5f;
        framing.m_ScreenY = 0.5f;
        framing.m_DeadZoneWidth = 0.18f;
        framing.m_DeadZoneHeight = 0.16f;
        framing.m_SoftZoneWidth = 0.68f;
        framing.m_SoftZoneHeight = 0.64f;

        gameplayCameraConfiner = virtualCameraObject.GetComponent<CinemachineConfiner2D>();
        if (gameplayCameraConfiner == null)
            gameplayCameraConfiner = virtualCameraObject.AddComponent<CinemachineConfiner2D>();
        gameplayCameraConfiner.m_Damping = 0.12f;
        gameplayCameraConfiner.m_MaxWindowSize = GameplayCameraSize;

        RefreshGameplayCameraRig();
    }

    private void RefreshGameplayCameraRig()
    {
        if (gameplayVirtualCamera == null)
            EnsureGameplayCameraRig();
        if (gameplayVirtualCamera == null)
            return;

        if (playerView != null)
            gameplayVirtualCamera.Follow = playerView.transform;

        RefreshGameplayCameraBounds();
        if (gameplayCameraConfiner != null)
        {
            gameplayCameraConfiner.m_BoundingShape2D = gameplayCameraBounds;
            gameplayCameraConfiner.InvalidateCache();
        }

        Camera camera = Camera.main;
        if (camera != null && playerView != null)
            camera.transform.position = new Vector3(playerView.transform.position.x, playerView.transform.position.y, -10f);
    }

    private void RefreshGameplayCameraBounds()
    {
        GameObject boundsObject = FindSceneObjectIncludingInactive(GameplayCameraBoundsName);
        if (boundsObject == null)
        {
            boundsObject = new GameObject(GameplayCameraBoundsName);
            boundsObject.transform.SetParent(transform);
        }

        gameplayCameraBounds = boundsObject.GetComponent<PolygonCollider2D>();
        if (gameplayCameraBounds == null)
            gameplayCameraBounds = boundsObject.AddComponent<PolygonCollider2D>();
        gameplayCameraBounds.isTrigger = true;
        gameplayCameraBounds.pathCount = 1;

        float minX = -0.5f - GameplayCameraBoundsPadding;
        float minY = -0.5f - GameplayCameraBoundsPadding;
        float maxX = (Width - 1) * CellSize + 0.5f + GameplayCameraBoundsPadding;
        float maxY = (Height - 1) * CellSize + 0.5f + GameplayCameraBoundsPadding;
        gameplayCameraBounds.SetPath(0, new[]
        {
            new Vector2(minX, minY),
            new Vector2(maxX, minY),
            new Vector2(maxX, maxY),
            new Vector2(minX, maxY),
        });
    }

}
