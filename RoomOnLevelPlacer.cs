using Cornel.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Cornel.LevelGenerating
{
	public class RoomOnLevelPlacer
	{
		private readonly Room _room = null;

		private RoomPreset Preset => _room.Preset;
		private Vector2Int Position => _room.Position;
		private List<RoomExit> Exits => _room.Exits;

		public RoomOnLevelPlacer(Room room)
		{
			_room = room;
		}

		public void Place(TilemapProvider tilemapProvider, TileProvider tileProvider,
			Action<Collider2D> onRoomEntered, Action<Collider2D> onRoomExited,
			out InRoomPrefabController inRoomPrefabController)
		{
			PlaceGrounds(tilemapProvider, tileProvider);
			PlaceDoors(tilemapProvider, tileProvider, onRoomEntered, onRoomExited);
			PlaceWalls(tilemapProvider, tileProvider);
			inRoomPrefabController = PlaceInRoomPrefab();
			PlaceRandomEnviro();
		}

		private readonly TileBase[] _groundTilesTmp = new TileBase[9];
		private void PlaceGrounds(TilemapProvider tilemapProvider, TileProvider tileProvider)
		{
			TileProvider.GroundType[] possibleFlags = Enum.GetValues(typeof(TileProvider.GroundType))
				.Cast<TileProvider.GroundType>()
				.Where(groundType => Preset.PossibleGroundTypes.HasFlag(groundType))
				.ToArray();

			int randomIndex = UnityEngine.Random.Range(0, possibleFlags.Length);
			var selectedGroundType = possibleFlags[randomIndex];

			foreach (var groundTile in Preset.Grounds)
			{
				var position = new Vector3Int(groundTile.X, groundTile.Y, 0);
				position += (Vector3Int)Position;

				var groundTileBase = tileProvider.GetGroundTile(selectedGroundType);
				if (UnityEngine.Random.Range(0f, 100f) < Preset.ChanceForBigGroundTile)
				{
					BoundsInt boundsInt = new(position + new Vector3Int(-1, -1), new Vector3Int(3, 3, 1));
					tilemapProvider.GroundTilemap.GetTilesBlockNonAlloc(boundsInt, _groundTilesTmp);
					if (_groundTilesTmp.All(tile => tile != tileProvider.BigGroundTile))
					{
						groundTileBase = tileProvider.BigGroundTile;
					}
				}

				tilemapProvider.GroundTilemap.SetTile(position, groundTileBase);
				tilemapProvider.OverGroundTilemap.SetTile(position, tileProvider.OverGroundTile);
				tilemapProvider.WallsNorthTilemap.SetTile(position, tileProvider.BlankGroundTile);
				tilemapProvider.WallsSouthTilemap.SetTile(position, tileProvider.BlankGroundTile);
			}
		}

		private void PlaceWalls(TilemapProvider tilemapProvider, TileProvider tileProvider)
		{
			TileProvider.WallType[] possibleFlags = Enum.GetValues(typeof(TileProvider.WallType))
				.Cast<TileProvider.WallType>()
				.Where(wallType => Preset.PossibleWallTypes.HasFlag(wallType))
				.ToArray();

			int randomIndex = UnityEngine.Random.Range(0, possibleFlags.Length);
			var selectedWallType = possibleFlags[randomIndex];
			var wallTile = tileProvider.GetWallTile(selectedWallType);

			for (int i = Preset.Walls.Length - 1; i >= 0; i--)
			{
				RoomPreset.TileWithChance wallTilePreset = Preset.Walls[i];
				if (RandomUtils.NextInt(0, 100) <= wallTilePreset.ChancePercent)
				{
					var position = new Vector3Int(wallTilePreset.X, wallTilePreset.Y, 0);
					position += (Vector3Int)Position;
					if (Exits.Any(exit => exit.LeftWorldPosition == (Vector2Int)position || exit.RightWorldPosition == (Vector2Int)position))
					{
						continue;
					}

					if ((tilemapProvider.GroundTilemap.GetTile(position + Vector3Int.down) == null) &&
						(tilemapProvider.GroundTilemap.GetTile(position + Vector3Int.up) != null ||
						tilemapProvider.WallsNorthTilemap.GetTile(position + Vector3Int.up) != null ||
						tilemapProvider.WallsSouthTilemap.GetTile(position + Vector3Int.up) != null))
					{
						tilemapProvider.WallsSouthTilemap.SetTile(position, wallTile);
						tilemapProvider.WallsNorthTilemap.SetTile(position, tileProvider.BlankWallTile);
					}
					else
					{
						tilemapProvider.WallsNorthTilemap.SetTile(position, wallTile);
						tilemapProvider.WallsSouthTilemap.SetTile(position, tileProvider.BlankWallTile);
					}

					tilemapProvider.OverGroundTilemap.SetTile(position, tileProvider.BlankWallTile);

					_room.Pathfinding.SetNode(wallTilePreset.X, wallTilePreset.Y, 1);
				}
			}
		}

		private void PlaceDoors(TilemapProvider tilemapProvider, TileProvider tileProvider, Action<Collider2D> onRoomEntered, Action<Collider2D> onRoomExited)
		{
			foreach (var exit in Exits)
			{
				var door = _room.DoorSpawner.Spawn(exit, _room);

				_room.Doors.Add(door);

				door.EnterTrigger.Entered += onRoomEntered;
				door.ExitTrigger.Entered += onRoomExited;

				tilemapProvider.GroundTilemap.SetTile((Vector3Int)exit.LeftWorldPosition, tileProvider.GetGroundTile(TileProvider.GroundType.Normal));
				tilemapProvider.WallsNorthTilemap.SetTile((Vector3Int)exit.LeftWorldPosition, tileProvider.BlankGroundTile);
				tilemapProvider.WallsSouthTilemap.SetTile((Vector3Int)exit.LeftWorldPosition, tileProvider.BlankGroundTile);

				tilemapProvider.GroundTilemap.SetTile((Vector3Int)exit.RightWorldPosition, tileProvider.GetGroundTile(TileProvider.GroundType.Normal));
				tilemapProvider.WallsNorthTilemap.SetTile((Vector3Int)exit.RightWorldPosition, tileProvider.BlankGroundTile);
				tilemapProvider.WallsSouthTilemap.SetTile((Vector3Int)exit.RightWorldPosition, tileProvider.BlankGroundTile);
			}
		}

		private InRoomPrefabController PlaceInRoomPrefab()
		{
			if (Preset.InRoomPrefab != null)
			{
				var inRoomPrefabController = GameObject.Instantiate(Preset.InRoomPrefab, _room.transform).GetComponent<InRoomPrefabController>();
				inRoomPrefabController.Initialize(_room);

				return inRoomPrefabController;
			}

			return null;
		}

		private void PlaceRandomEnviro()
		{
			var randomEnviroHolder = Preset.RandomEnviroHolder;
			if (randomEnviroHolder == null || randomEnviroHolder.IsNull)
			{
				return;
			}

			foreach (var groundTile in Preset.Grounds)
			{
				var position = new Vector2(groundTile.X, groundTile.Y);
				position += (Vector2)Position;
				position += UnityEngine.Random.insideUnitCircle * randomEnviroHolder.MaxOffset;

				if (UnityEngine.Random.Range(0f, 100f) < randomEnviroHolder.SpawnChance)
				{
					var spawnData = randomEnviroHolder.GetRandomSpawnData();
					GameObject.Instantiate(spawnData.GetRandomPrefab(), position, spawnData.GetRandomRotation(), _room.transform);
				}
			}
		}
	}
}