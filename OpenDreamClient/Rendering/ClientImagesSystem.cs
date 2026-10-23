using System.Diagnostics.CodeAnalysis;
using OpenDreamShared.Rendering;

namespace OpenDreamClient.Rendering;

internal sealed partial class ClientImagesSystem : SharedClientImagesSystem {
    [Dependency] private IEntityManager _entityManager = default!;

    private readonly Dictionary<Vector3, List<NetEntity>> _turfClientImages = [];
    // By NetEntity, since the movable may not have been sent to us yet
    private readonly Dictionary<NetEntity, List<NetEntity>> _amClientImages = [];

    public override void Initialize() {
        SubscribeNetworkEvent<AddClientImageEvent>(OnAddClientImage);
        SubscribeNetworkEvent<RemoveClientImageEvent>(OnRemoveClientImage);
    }

    public override void Shutdown() {
        _turfClientImages.Clear();
        _amClientImages.Clear();
    }

    public bool TryGetClientImages(EntityUid entity, Vector3? tileCoords, [NotNullWhen(true)] out List<NetEntity>? result) {
        if(entity == EntityUid.Invalid && tileCoords is not null) {
            if (!_turfClientImages.TryGetValue(tileCoords.Value, out result))
                return false;
        } else {
            if (!_amClientImages.TryGetValue(_entityManager.GetNetEntity(entity), out result))
                return false;
        }

        return result.Count > 0;
    }

    private void OnAddClientImage(AddClientImageEvent e) {
        if (!e.AttachedEntity.IsValid()) {
            if (!_turfClientImages.TryGetValue(e.TurfCoords, out var iconList))
                iconList = new List<NetEntity>();
            iconList.Add(e.ImageEntity);
            _turfClientImages[e.TurfCoords] = iconList;
        } else {
            if (!_amClientImages.TryGetValue(e.AttachedEntity, out var iconList))
                iconList = new List<NetEntity>();
            iconList.Add(e.ImageEntity);
            _amClientImages[e.AttachedEntity] = iconList;
        }
    }

    private void OnRemoveClientImage(RemoveClientImageEvent e) {
        if (!e.AttachedEntity.IsValid()) {
            if (!_turfClientImages.TryGetValue(e.TurfCoords, out var iconList))
                return;
            iconList.Remove(e.ImageEntity);
            if (iconList.Count == 0)
                _turfClientImages.Remove(e.TurfCoords);
        } else {
            if (!_amClientImages.TryGetValue(e.AttachedEntity, out var iconList))
                return;
            iconList.Remove(e.ImageEntity);
            if (iconList.Count == 0)
                _amClientImages.Remove(e.AttachedEntity);
        }
    }
}
