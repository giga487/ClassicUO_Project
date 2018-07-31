﻿using System.Collections.Generic;
using System.Linq;
using ClassicUO.AssetsLoader;
using ClassicUO.Game.Renderer.Views;
using ClassicUO.Game.WorldObjects;
using ClassicUO.Utility;

namespace ClassicUO.Game.Map
{
    public sealed class Tile : WorldObject
    {
        private static readonly List<WorldObject> _itemsAtZ = new List<WorldObject>();
        private readonly List<WorldObject> _objectsOnTile;
        private bool _needSort;

        public Tile() : base(World.Map)
        {
            _objectsOnTile = new List<WorldObject>();
            _objectsOnTile.Add(this);
        }


        public IReadOnlyList<WorldObject> ObjectsOnTiles
        {
            get
            {
                if (_needSort)
                {
                    Sort();
                    _needSort = false;
                }

                return _objectsOnTile;
            }
        }

        public override Position Position { get; set; }
        public new TileView ViewObject => (TileView) base.ViewObject;
        public bool IsIgnored => Graphic < 3 || Graphic == 0x1DB || Graphic >= 0x1AE && Graphic <= 0x1B5;

        public LandTiles TileData => AssetsLoader.TileData.LandData[Graphic];

        public void AddWorldObject(in WorldObject obj)
        {
            _objectsOnTile.Add(obj);

            _needSort = true;
        }

        public void RemoveWorldObject(in WorldObject obj)
        {
            _objectsOnTile.Remove(obj);
        }

        public void ForceSort() => _needSort = true;

        public void Clear()
        {
            _objectsOnTile.Clear();
            _objectsOnTile.Add(this);
            DisposeView();
            Graphic = 0;
            Position = Position.Invalid;
        }

        public void Sort()
        {
            //_objectsOnTile.Sort((s, a) => s.ViewObject.DepthValue.CompareTo(a.ViewObject.DepthValue));

            for (int i = 0; i < _objectsOnTile.Count - 1; i++)
            {
                int j = i + 1;
                while (j > 0)
                {
                    int result = Compare(_objectsOnTile[j - 1], _objectsOnTile[j]);
                    if (result > 0)
                    {
                        WorldObject temp = _objectsOnTile[j - 1];
                        _objectsOnTile[j - 1] = _objectsOnTile[j];
                        _objectsOnTile[j] = temp;
                    }

                    j--;
                }
            }
        }

        public List<WorldObject> GetItemsBetweenZ(in int z0, in int z1)
        {
            List<WorldObject> items = _itemsAtZ;
            _itemsAtZ.Clear();

            for (int i = 0; i < ObjectsOnTiles.Count; i++)
                if (MathHelper.InRange(ObjectsOnTiles[i].Position.Z, z0, z1))
                    if (ObjectsOnTiles[i] is Item || ObjectsOnTiles[i] is Static)
                        items.Add(ObjectsOnTiles[i]);
            return items;
        }

        public bool IsZUnderObjectOrGround(in sbyte z, out WorldObject entity, out WorldObject ground)
        {
            List<WorldObject> list = _objectsOnTile;

            entity = null;
            ground = null;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Position.Z <= z)
                    continue;

                if (list[i] is Item it)
                {
                    StaticTiles itemdata = it.ItemData;

                    if (AssetsLoader.TileData.IsRoof((long) itemdata.Flags) ||
                        AssetsLoader.TileData.IsSurface((long) itemdata.Flags) ||
                        AssetsLoader.TileData.IsWall((long) itemdata.Flags) &&
                        AssetsLoader.TileData.IsImpassable((long) itemdata.Flags))
                        if (entity == null || list[i].Position.Z < entity.Position.Z)
                            entity = list[i];
                }
                else if (list[i] is Static st)
                {
                    StaticTiles itemdata = st.ItemData;

                    if (AssetsLoader.TileData.IsRoof((long) itemdata.Flags) ||
                        AssetsLoader.TileData.IsSurface((long) itemdata.Flags) ||
                        AssetsLoader.TileData.IsWall((long) itemdata.Flags) &&
                        AssetsLoader.TileData.IsImpassable((long) itemdata.Flags))
                        if (entity == null || list[i].Position.Z < entity.Position.Z)
                            entity = list[i];
                }
                else if (list[i] is Tile tile && tile.ViewObject.SortZ >= z + 12)
                {
                    ground = list[i];
                }
            }

            return entity != null || ground != null;
        }

        public T[] GetWorldObjects<T>() where T : WorldObject
        {
            return _objectsOnTile.OfType<T>().ToArray();
        }

        // create view only when TileID is initialized
        protected override View CreateView()
        {
            return Graphic <= 0 ? null : new TileView(this);
        }


        private static int Compare(in WorldObject x, in WorldObject y)
        {
            (int xZ, int xType, int xThreshold, int xTierbreaker) = GetSortValues(x);
            (int yZ, int yType, int yThreshold, int yTierbreaker) = GetSortValues(y);

            xZ += xThreshold;
            yZ += yThreshold;

            int comparison = xZ - yZ;
            if (comparison == 0)
                comparison = xType - yType;
            if (comparison == 0)
                comparison = xThreshold - yThreshold;
            if (comparison == 0)
                comparison = xTierbreaker - yTierbreaker;

            return comparison;
        }

        private static (int, int, int, int) GetSortValues(in WorldObject e)
        {
            if (e is Tile tile)
                return (tile.ViewObject.SortZ,
                    0,
                    0,
                    0);

            if (e is Static staticitem)
            {
                StaticTiles itemdata = AssetsLoader.TileData.StaticData[staticitem.Graphic];

                return (staticitem.Position.Z,
                    1,
                    (itemdata.Height > 0 ? 1 : 0) + (AssetsLoader.TileData.IsBackground((long) itemdata.Flags) ? 0 : 1),
                    staticitem.Index);
            }

            if (e is Item item)
                return (item.Position.Z,
                    item.IsCorpse ? 4 : 2,
                    (item.ItemData.Height > 0 ? 1 : 0) +
                    (AssetsLoader.TileData.IsBackground((long) item.ItemData.Flags) ? 0 : 1),
                    (int) item.Serial.Value);
            if (e is Mobile mobile)
                return (mobile.Position.Z,
                    3 /* is sitting */,
                    2,
                    mobile == World.Player ? 0x40000000 : (int) mobile.Serial.Value);
            if (e is DeferredEntity def)
                return (def.Position.Z,
                    2, 1, 0);

            return (0, 0, 0, 0);
        }
    }
}