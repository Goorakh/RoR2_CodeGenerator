// Written out here for easier editing, should not be present in the build, but should be appended to the generated code
/*
internal class AddressableAssetRef<T> where T : UnityEngine.Object
{
    public readonly string Key;

    bool _hasLoadedAsset;
    T _cachedAsset;

    public T Asset
    {
        get
        {
            if (!_hasLoadedAsset)
            {
                _cachedAsset = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(Key).WaitForCompletion();
                _hasLoadedAsset = true;
            }

            return _cachedAsset;
        }
    }

    internal AddressableAssetRef(string key)
    {
        Key = key;
    }
}
*/