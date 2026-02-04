using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Cornel.Utils;

namespace Cornel.LevelGenerating
{
	[CreateAssetMenu(fileName = "RoomPreset", menuName = "Scriptable Objects/Level Generation/RoomPreset")]
	public class RoomPreset : ScriptableObject
	{
		[Serializable]
		public class Tile
		{
			[field: SerializeField]
			public int X { get; set; } = 0;

			[field: SerializeField]
			public int Y { get; set; } = 0;

			[ShowInInspector, ReadOnly]
			public Vector2Int Position => new(X, Y);

			public Tile(int x, int y)
            {
				X = x;
				Y = y;
            }
        }

		[Serializable]
		public class TileWithChance : Tile
		{
			[field: SerializeField, ShowInInspector, ReadOnly]
			public float ChancePercent { get; set; } = 100f;

			public TileWithChance(int x, int y, float chancePercent = 100f) : base(x, y)
            {
				ChancePercent = chancePercent;
            }
        }

		[Serializable]
		public class TilesWithPrefab
		{
			[field: SerializeField]
			public TileWithChance[] Tiles { get; set; } = null;

			[field: SerializeField]
			public GameObject Prefab { get; set; } = null;

            public TilesWithPrefab(TileWithChance[] tiles, GameObject prefab)
            {
				Tiles = tiles;
				Prefab = prefab;
            }
        }

		[Serializable]
		public class ExitPreset
		{
			[field: SerializeField]
			public int LeftX { get; private set; } = 0;

			[field: SerializeField]
			public int LeftY { get; private set; } = 0;

			[field: SerializeField]
			public int RightX { get; private set; } = 0;

			[field: SerializeField]
			public int RightY { get; private set; } = 0;

			[ShowInInspector, ReadOnly]
			public Vector2Int LeftPosition => new(LeftX, LeftY);

			[ShowInInspector, ReadOnly]
			public Vector2Int RightPosition => new(RightX, RightY);

			[field: SerializeField, ShowInInspector, ReadOnly]
			public Vector2Int Direction { get; set; } = Vector2Int.zero;

            public ExitPreset(int firstX, int firstY, int secondX, int secondY, Vector2Int direction)
            {
				if (direction.x > 0)
				{
					LeftX = RightX = firstX;
					LeftY = Mathf.Max(firstY, secondY);
					RightY = Mathf.Min(firstY, secondY);
				}
				else if (direction.x < 0)
				{
					LeftX = RightX = firstX;
					LeftY = Mathf.Min(firstY, secondY);
					RightY = Mathf.Max(firstY, secondY);
				}
				else if (direction.y > 0)
				{
					LeftY = RightY = firstY;
					LeftX = Mathf.Min(firstX, secondX);
					RightX = Mathf.Max(firstX, secondX);
				}
				else
				{
					LeftY = RightY = firstY;
					LeftX = Mathf.Max(firstX, secondX);
					RightX = Mathf.Min(firstX, secondX);
				}

				Direction = direction;
            }
        }

		[field: SerializeField, ShowInInspector, ReadOnly]
		public int Width { get; set; } = 0;

		[field: SerializeField, ShowInInspector, ReadOnly]
		public int Height { get; set; } = 0;

		public Vector2Int Size => new(Width, Height);

		[field: SerializeField, ShowInInspector, ReadOnly]
		public Tile[] Grounds { get; set; } = null;

		[field: SerializeField, ShowInInspector, ReadOnly]
		public TileWithChance[] Walls { get; set; } = null;
		
		[field: SerializeField, ShowInInspector, ReadOnly]
		public ExitPreset[] PossibleExits { get; set; } = null;

		[field: SerializeField, ShowInInspector, ReadOnly]
		public TileProvider.GroundType PossibleGroundTypes { get; set; } = TileProvider.GroundType.Normal;

		[field: SerializeField, ShowInInspector, ReadOnly]
		public TileProvider.WallType PossibleWallTypes { get; set; } = TileProvider.WallType.Normal;

		[field: SerializeField, ShowInInspector, ReadOnly]
		public float ChanceForBigGroundTile { get; set; } = 10f;

		[field: SerializeField, ShowInInspector, ReadOnly]
		public EnemiesSetData[] EnemiesSetsData { get; set; } = null;

		[field: SerializeField, ShowInInspector, ReadOnly]
		public GameObject InRoomPrefab { get; set; } = null;

		[field: SerializeField, ShowInInspector, ReadOnly]
		public RandomEnviroHolder RandomEnviroHolder { get; set; } = null;

		public bool TryGetExitPreset(Vector2Int incomingDirection, out ExitPreset exitPreset, bool forceMatchDirection)
		{
			List<int> possibleIds = Enumerable.Range(0, PossibleExits.Length).ToList();
			while (possibleIds.Count > 0)
			{
				var id = possibleIds[RandomUtils.NextInt(0, possibleIds.Count)];
				var possibleExit = PossibleExits[id];
				if (forceMatchDirection)
				{
					if (possibleExit.Direction.x * incomingDirection.x < 0 || possibleExit.Direction.y * incomingDirection.y < 0)
					{
						exitPreset = possibleExit;
						return true;
					}
				}
				else
				{
					if (incomingDirection.x != 0 && (possibleExit.LeftX - Mathf.FloorToInt(Width / 2f)) * incomingDirection.x < 0)
					{
						exitPreset = possibleExit;
						return true;
					}
					else if (incomingDirection.y != 0 && (possibleExit.LeftY - Mathf.FloorToInt(Height / 2f)) * incomingDirection.y < 0)
					{
						exitPreset = possibleExit;
						return true;
					}
				}

				possibleIds.Remove(id);
			}

			exitPreset = null;
			return false;
		}
	}
}