using UnityEngine;

namespace SeaweedSlayer
{
    // Feedback lives under the arena, so pooled items/enemies can immediately be hidden.
    public sealed class SeaweedFeedback : MonoBehaviour
    {
        const float Lifetime = .85f;
        static Sprite pixelSprite;
        TextMesh[] labels;
        Transform[] pixels;
        SpriteRenderer[] pixelRenderers;
        Vector3[] velocities;
        float age;

        public static void ShowScore(Vector3 position, int points, Font font, Transform parent)
        {
            var root = new GameObject("Score +" + points);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var feedback = root.AddComponent<SeaweedFeedback>();
            feedback.labels = new TextMesh[9];
            for (int i = 0; i < feedback.labels.Length; i++)
            {
                var go = new GameObject(i == 8 ? "White text" : "Black outline");
                go.transform.SetParent(root.transform, false);
                if (i < 8)
                {
                    float angle = i * Mathf.PI / 4;
                    go.transform.localPosition = new Vector3(Mathf.Cos(angle) * .025f, Mathf.Sin(angle) * .025f, .01f);
                }
                var text = go.AddComponent<TextMesh>();
                text.text = "+" + points;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.fontSize = 64;
                text.characterSize = .13f;
                text.color = i == 8 ? Color.white : Color.black;
                if (font != null)
                {
                    text.font = font;
                    go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                }
                go.GetComponent<MeshRenderer>().sortingOrder = i == 8 ? 101 : 100;
                feedback.labels[i] = text;
            }
            feedback.FaceCamera();
        }

        public static void PixelBurst(Vector3 position, Color color, Material material, Transform parent)
        {
            if (pixelSprite == null)
                pixelSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            var root = new GameObject("Enemy pixel burst");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var feedback = root.AddComponent<SeaweedFeedback>();
            const int count = 24;
            feedback.pixels = new Transform[count];
            feedback.pixelRenderers = new SpriteRenderer[count];
            feedback.velocities = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Pixel");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = Random.insideUnitSphere * .25f;
                go.transform.localScale = Vector3.one * Random.Range(.08f, .18f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = pixelSprite;
                if (material != null) renderer.sharedMaterial = material;
                renderer.color = i % 4 == 0 ? Color.white : color;
                renderer.sortingOrder = 20;
                feedback.pixels[i] = go.transform;
                feedback.pixelRenderers[i] = renderer;
                feedback.velocities[i] = Random.insideUnitSphere * 3 + Vector3.up * 2;
            }
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= Lifetime) { Destroy(gameObject); return; }
            float alpha = 1 - Mathf.InverseLerp(.35f, Lifetime, age);
            if (labels != null)
            {
                transform.position += Vector3.up * (Time.deltaTime * 1.2f);
                FaceCamera();
                foreach (var label in labels)
                { var color = label.color; color.a = alpha; label.color = color; }
            }
            if (pixels != null)
            {
                var camera = Camera.main;
                for (int i = 0; i < pixels.Length; i++)
                {
                    velocities[i] += Vector3.down * (Time.deltaTime * 5);
                    pixels[i].position += velocities[i] * Time.deltaTime;
                    if (camera != null) pixels[i].rotation = camera.transform.rotation;
                    var color = pixelRenderers[i].color; color.a = alpha; pixelRenderers[i].color = color;
                }
            }
        }
        void FaceCamera()
        { if (Camera.main != null) transform.rotation = Camera.main.transform.rotation; }
    }
}
