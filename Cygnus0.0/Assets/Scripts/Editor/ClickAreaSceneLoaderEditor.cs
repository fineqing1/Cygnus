using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public static class ClickAreaSceneLoaderEditor
{
    [MenuItem("GameObject/UI/Click Area (Load Bigball)", false, 2100)]
    static void CreateClickArea(MenuCommand command)
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("未找到 Canvas，请先创建 Canvas 再使用此菜单。");
            return;
        }

        GameObject go = new GameObject("ClickArea_Bigball");
        Undo.RegisterCreatedObjectUndo(go, "Create Click Area");
        go.transform.SetParent(canvas.transform, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(200, 80);
        rt.anchoredPosition = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.01f);
        img.raycastTarget = true;

        var loader = go.AddComponent<ClickAreaSceneLoader>();
        loader.sceneName = "Bigball";

        Selection.activeGameObject = go;
    }
}
