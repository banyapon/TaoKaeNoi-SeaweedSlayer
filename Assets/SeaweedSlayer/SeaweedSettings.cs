using UnityEngine;

namespace SeaweedSlayer
{
    [CreateAssetMenu(menuName = "Seaweed Slayer/Settings")]
    public sealed class SeaweedSettings : ScriptableObject
    {
        [Tooltip("Public HTTPS address of the mobile WebGL controller.")]
        public string controllerUrl = "https://taokaenoiarena.vercel.app/Controller";
        [Range(1, 20)] public int maximumPlayers = 20;
        [Min(10)] public float roundSeconds = 90;
        public Font titleFont, uiFont;
        public Sprite playerOutline, snack, logo;
        public GameObject joystickPrefab, playerModel, enemyModel;
        [Range(1, 4)] public int maximumEnemies = 3;
        [Min(1)] public int enemyScore = 50;
        public GameObject[] forestTrees;
        public GameObject waterPrefab, groundPrefab, grassPrefab;
        public Material greenA, greenB, wood, water, player, projectile;
        // Red, gold, green, blue, yellow, pink, brown, purple, orange, sky blue.
        public Color[] palette = {
            new Color32(255, 95, 105, 255), new Color32(218, 165, 32, 255),
            new Color32(99, 225, 149, 255), new Color32(55, 105, 230, 255),
            new Color32(255, 231, 100, 255), new Color32(255, 126, 190, 255),
            new Color32(153, 95, 55, 255), new Color32(160, 95, 220, 255),
            new Color32(255, 145, 50, 255), new Color32(78, 203, 255, 255)
        };
    }
}
