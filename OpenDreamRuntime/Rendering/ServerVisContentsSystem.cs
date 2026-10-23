using OpenDreamRuntime.Objects.Types;
using Robust.Server.GameStates;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Player;

namespace OpenDreamRuntime.Rendering;

/// <summary>
/// Sends vis_contents only to the clients that can see their holder via ExpandPvsEvent
/// TODO: Copies some stuff from PVS, we should expose visible chunks through like ExpandPvsEvent and/or a public GetVisibleChunks
///       Would let us remove AddViewer and UpdateViewSize here
/// </summary>
public sealed partial class ServerVisContentsSystem : EntitySystem {
    [Dependency] private AtomManager _atomManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DreamManager _dreamManager = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;

    private const int ChunkSize = ChunkEntitySystem.ChunkSize;

    // vis_contents lists of atoms on the map, by the chunk the atom is in
    private readonly Dictionary<(MapId, Vector2i), HashSet<DreamVisContentsList>> _holders = [];
    // the reverse
    private readonly Dictionary<DreamVisContentsList, (MapId, Vector2i)> _holderChunks = [];
    // ExpandPvsEvent is raised for sessions in parallel
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<ICommonSession, SessionData> _sessionData = [];
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<EyeComponent> _eyeQuery;
    private float _viewSize;

    private sealed class SessionData {
        public readonly List<EntityUid> Entities = [];
        public readonly HashSet<DreamObjectAtom> Visited = [];
    }

    public override void Initialize() {
        _xformQuery = GetEntityQuery<TransformComponent>();
        _eyeQuery = GetEntityQuery<EyeComponent>();

        Subs.CVar(_cfg, CVars.NetMaxUpdateRange, _ => UpdateViewSize());
        Subs.CVar(_cfg, CVars.NetPvsPriorityRange, _ => UpdateViewSize(), true);
        SubscribeLocalEvent<ExpandPvsEvent>(OnExpandPvs);
    }

    /// <summary>
    /// Call whenever a vis_contents list's length changes or its owner moves
    /// </summary>
    public void UpdateHolder(DreamVisContentsList list) {
        // Images and movables inside other atoms aren't drawn on the map
        var onMap = list.Owner is DreamObjectTurf or DreamObjectMovable { Loc: DreamObjectTurf or DreamObjectArea };

        var chunk = onMap && list.GetLength() != 0 && !list.Owner.Deleted ? GetChunk((DreamObjectAtom)list.Owner) : null;

        if (_holderChunks.TryGetValue(list, out var oldChunk)) {
            if (chunk == oldChunk)
                return;

            _holderChunks.Remove(list);
            var oldHolders = _holders[oldChunk];
            oldHolders.Remove(list);
            if (oldHolders.Count == 0)
                _holders.Remove(oldChunk);
        }

        if (chunk.HasValue) {
            var newChunk = chunk.Value;
            _holderChunks[list] = newChunk;
            if (!_holders.TryGetValue(newChunk, out var holders))
                _holders[newChunk] = holders = [];

            holders.Add(list);
        }
    }

    /// <summary>
    /// The PVS chunk an atom is drawn in, or null if it's in nullspace
    /// </summary>
    private (MapId, Vector2i)? GetChunk(DreamObjectAtom atom) {
        // A movable's position is its outermost container's position
        var (x, y, z) = _atomManager.GetAtomPosition(atom);
        return z != 0 ? (new MapId(z), new Vector2i(x, y) / ChunkSize) : null;
    }

    private void OnExpandPvs(ref ExpandPvsEvent args) {
        var session = args.Session;
        var data = _sessionData.GetOrCreateValue(session);
        data.Entities.Clear();
        data.Visited.Clear();

        if (session.AttachedEntity is { } attached)
            AddViewer(attached, data);
        foreach (var viewer in session.ViewSubscriptions) {
            if (viewer != session.AttachedEntity)
                AddViewer(viewer, data);
        }

        // client.screen and client.images are visible wherever they are
        if (_dreamManager.GetConnectionBySession(session).Client is { } client) {
            // TODO: foreach EnumerateValues() boxes the enumerator every tick, for every client
            foreach (var screenObject in client.Screen.EnumerateValues()) {
                if (screenObject.TryGetValueAsDreamObject<DreamObjectAtom>(out var atom) && atom.VisContentsList is { } visContents)
                    AddVisContents(visContents, data);
            }

            foreach (var image in client.Images.EnumerateValues()) {
                if (image.TryGetValueAsDreamObject<DreamObjectImage>(out var imageObject) && imageObject.VisContentsList is { } visContents)
                    AddVisContents(visContents, data);
            }
        }

        if (data.Entities.Count == 0)
            return;

        if (args.Entities == null)
            args.Entities = data.Entities;
        else
            args.Entities.AddRange(data.Entities);
    }

    // Copied from RT
    // View bounds: https://github.com/space-wizards/RobustToolbox/blob/v288.1.0/Robust.Server/GameStates/PvsSystem.cs#L389-L404
    // Chunk enumeration: https://github.com/space-wizards/RobustToolbox/blob/v288.1.0/Robust.Server/GameStates/PvsSystem.Chunks.cs#L114-L137
    private void AddViewer(EntityUid viewer, SessionData data) {
        if (!_xformQuery.TryComp(viewer, out var xform) || xform.MapID == MapId.Nullspace)
            return;

        var pos = _transformSystem.GetWorldPosition(xform);
        var size = _viewSize;
        if (_eyeQuery.TryComp(viewer, out var eye)) {
            pos += eye.Offset;
            size *= eye.PvsScale;
        }

        var chunks = new ChunkIndicesEnumerator(pos, Math.Max(size, 1) / 2f, ChunkSize);
        while (chunks.MoveNext(out var chunk)) {
            if (!_holders.TryGetValue((xform.MapID, chunk.Value), out var holders))
                continue;

            foreach (var holder in holders)
                AddVisContents(holder, data);
        }
    }

    private static void AddVisContents(DreamVisContentsList list, SessionData data) {
        var visContents = list.VisContents;
        for (int i = 0; i < visContents.Count; i++) {
            var visContent = visContents[i];
            if (visContent.Deleted || !data.Visited.Add(visContent))
                continue;

            if (visContent is DreamObjectMovable movable)
                data.Entities.Add(movable.Entity);
            if (visContent.VisContentsList is { } nested) // Nested vis_contents are visible wherever this is
                AddVisContents(nested, data);
        }
    }

    // Copied from RT
    // https://github.com/space-wizards/RobustToolbox/blob/v288.1.0/Robust.Server/GameStates/PvsSystem.cs#L273-L282
    private void UpdateViewSize() {
        _viewSize = Math.Max(Math.Max(ChunkSize, _cfg.GetCVar(CVars.NetMaxUpdateRange)), _cfg.GetCVar(CVars.NetPvsPriorityRange));
    }
}
