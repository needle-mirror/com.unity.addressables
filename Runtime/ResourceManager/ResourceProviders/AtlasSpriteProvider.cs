using System.ComponentModel;
using UnityEngine.U2D;

namespace UnityEngine.ResourceManagement.ResourceProviders
{
    /// <summary>
    /// Provides sprites from atlases
    /// </summary>
    [DisplayName("Sprites from Atlases Provider")]
    public class AtlasSpriteProvider : ResourceProviderBase
    {   
        /// <inheritdoc/>
        public override void Provide(ProvideHandle providerInterface)
        {
            var atlas = providerInterface.GetDependency<SpriteAtlas>(0);
            if (atlas == null)
            {
                providerInterface.Complete<Sprite>(null, false, new System.Exception(string.Format("Sprite atlas failed to load for location {0}.", providerInterface.Location.PrimaryKey)));
                return;
            }
            
            var sprite = atlas.GetSprite(providerInterface.ResourceManager.TransformInternalId(providerInterface.Location));
            if (sprite == null)
            {
                providerInterface.Complete<Sprite>(null, false, new System.Exception(string.Format("Sprite failed to load for location {0}.", providerInterface.Location.PrimaryKey)));
                return;
            }
            providerInterface.Complete(sprite, sprite != null, null);
        }
    }
}