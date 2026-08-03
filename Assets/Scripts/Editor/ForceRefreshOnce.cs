#if UNITY_EDITOR
using UnityEditor;
[InitializeOnLoad]
public static class ForceRefreshOnce {
  static ForceRefreshOnce() {
    AssetDatabase.Refresh();
  }
}
#endif
