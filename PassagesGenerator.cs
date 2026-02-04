using Cornel.Utils;
using Sirenix.OdinInspector;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Cornel.LevelGenerating
{
	public class PassagesGenerator : MonoBehaviour
	{
		[SerializeField, Required, AssetsOnly]
		private GameObject _passagePrefab = null;

		[SerializeField, MinValue(1)]
		private int _maxPassageLength = 20;

		private TilemapProvider _tilemapProvider = null;
		private TileProvider _tileProvider = null;
		private LevelController _levelController = null;

		public void Initialize(TilemapProvider tilemapProvider, TileProvider tileProvider, LevelController levelController)
		{
			_tilemapProvider = tilemapProvider;
			_tileProvider = tileProvider;
			_levelController = levelController;
		}

		public async Task<bool> TryCreatePassagesBetweenRooms(Transform passagesHolder, Dictionary<uint, Room> idsToPlacedRooms,
			float pauseBetweenActions)
		{
			int longestPassageLength = 0;

			foreach (var room in idsToPlacedRooms.Values)
			{
				foreach (var exit in room.Exits)
				{
					if (exit.OutgoingExits.Count <= 0)
					{
						continue;
					}

					int freeSpace = Room.FREE_SPACE_SIZE;
					var connectedExit = exit.OutgoingExits[0];
					// We actually want to check if there aren't any walls "outside" the normal boundaries so we are adding freeSpace
					var pivotPosition = new Vector3Int(
						Mathf.Min(exit.MinWorldPosition.x, connectedExit.MinWorldPosition.x) - freeSpace,
						Mathf.Min(exit.MinWorldPosition.y, connectedExit.MinWorldPosition.y) - freeSpace);

					var pathfinding = SetupPathfinding(_tilemapProvider, _tileProvider, exit, freeSpace, connectedExit, pivotPosition);

					// We don't need the passage to actually reach the exit tile - just 1 tile in front
					// The path won't reach the exit because of unpassable surrounding so we need to reach freeSpace in front of exit tile.
					var exitTargetPosition = exit.LeftWorldPosition - (Vector2Int)pivotPosition + exit.Preset.Direction * (freeSpace + 1);
					var connectedExitTargetPosition = connectedExit.RightWorldPosition - (Vector2Int)pivotPosition + connectedExit.Preset.Direction * (freeSpace + 1);
					var path = pathfinding.CreatePath(exitTargetPosition, connectedExitTargetPosition,
							minimalizeTurns: true, firstNodeDirection: exit.Preset.Direction, width: 2, canSelectNeighborEndNode: false);

					if (path == null)
					{
						Debug.LogWarning("Path not found when trying to generate a passage.");
						return false;
					}

					if (path.Count > _maxPassageLength)
					{
						Debug.LogWarning($"The found path for a passage is too long ({path.Count} tiles).");
						return false;
					}

					if (path.Count > longestPassageLength)
					{
						longestPassageLength = path.Count;
					}

					var isPathConnected = TryConnectPathToExits(exit, freeSpace, connectedExit, pathfinding, exitTargetPosition, connectedExitTargetPosition, path);

					if (!isPathConnected)
					{
						Debug.LogWarning("Couldn't connect exits when creating a passage!");
						return false;
					}

					var passage = Instantiate(_passagePrefab, passagesHolder).GetComponent<Passage>();
					await PlaceTiles(pivotPosition, path, pauseBetweenActions, passage);

					Vector2 startPosition = exit.CenterWorldPosition + exit.Preset.Direction;
					//Vector2 startPosition = new(path[0].WorldPosition.x + pivotPosition.x, path[0].WorldPosition.y + pivotPosition.y);
					Vector2 endPosition = connectedExit.CenterWorldPosition + connectedExit.Preset.Direction;
					//Vector2 endPosition = new(path[^1].WorldPosition.x + pivotPosition.x, path[^1].WorldPosition.y + pivotPosition.y);

					passage.Initialize(_levelController, exit, connectedExit, startPosition, endPosition);
					room.OutgoingPassages.Add(passage);
				}
			}

			Debug.Log($"Longest passage is {longestPassageLength} tiles.");

			return true;
		}

		private AStarPathfinding SetupPathfinding(TilemapProvider tilemapProvider, TileProvider tileProvider,
			RoomExit exit, int freeSpace, RoomExit connectedExit, Vector3Int pivotPosition)
		{
			var width = Mathf.Abs(exit.LeftWorldPosition.x - connectedExit.RightWorldPosition.x) + freeSpace * 2 + 1;
			var height = Mathf.Abs(exit.LeftWorldPosition.y - connectedExit.RightWorldPosition.y) + freeSpace * 2 + 1;

			var aStar = new AStarPathfinding(width, height);

			// Set all nodes as passable
			aStar.SetAllNodes(0);

			// Now mark walls and their surroundings as unpassable
			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < height; y++)
				{
					Vector3Int worldPosition = pivotPosition + new Vector3Int(x, y);
					var tile = tilemapProvider.WallsNorthTilemap.GetTile(worldPosition);
					if (tile is WallRuleTile || tile == tileProvider.BlankWallTile)
					{
						aStar.SetNodesBlock(x - freeSpace, y - freeSpace, x + freeSpace, y + freeSpace, 1);
						continue;
					}
				}
			}

			aStar.AddNeighbors(canBeDiagonal: false);
			return aStar;
		}

		private bool TryConnectPathToExits(RoomExit exit, int freeSpace, RoomExit connectedExit, AStarPathfinding pathfinding,
			Vector2Int exitTargetPosition, Vector2Int connectedExitTargetPosition, List<Node> path)
		{
			// Connect the path to the exits
			for (int i = 1; i < freeSpace + 1; i++)
			{
				var nodePosition = exitTargetPosition - exit.Preset.Direction * i;
				var node = pathfinding.GetNode(nodePosition.x, nodePosition.y);
				if (node == null)
				{
					return false;
				}

				node.Direction = exit.Preset.Direction;
				path.Insert(0, node);
			}

			for (int i = 1; i < freeSpace + 1; i++)
			{
				var nodePosition = connectedExitTargetPosition - connectedExit.Preset.Direction * i;
				var node = pathfinding.GetNode(nodePosition.x, nodePosition.y);
				if (node == null)
				{
					return false;
				}

				node.Direction = -connectedExit.Preset.Direction;
				path.Add(node);
			}

			return true;
		}

		private async Task PlaceTiles(Vector3Int pivotPosition, List<Node> path, float pauseBetweenActions, Passage passage)
		{
			Vector2Int? previousNodeDirection = path[0].Direction;
			foreach (var node in path)
			{
				Vector3Int worldPosition = pivotPosition + new Vector3Int(node.X, node.Y);
				Vector3Int nodeOnRightWorldPosition = worldPosition + (Vector3Int)node.Direction.Value.RotateRight();
				SetGroundTile(worldPosition, passage);
				SetGroundTile(nodeOnRightWorldPosition, passage);
				var newNodeDirection = node.Direction;

				if (previousNodeDirection == newNodeDirection)
				{
					// Just place walls on x or y axis
					if (node.Direction.Value.x == 1)
					{
						SetWallTile(worldPosition + Vector3Int.up, passage, _tilemapProvider.WallsNorthTilemap, _tilemapProvider.WallsSouthTilemap);
						SetWallTile(worldPosition + Vector3Int.down * 2, passage, _tilemapProvider.WallsSouthTilemap, _tilemapProvider.WallsNorthTilemap);
					}
					else if (node.Direction.Value.x == -1)
					{
						SetWallTile(worldPosition + Vector3Int.up * 2, passage, _tilemapProvider.WallsNorthTilemap, _tilemapProvider.WallsSouthTilemap);
						SetWallTile(worldPosition + Vector3Int.down, passage, _tilemapProvider.WallsSouthTilemap, _tilemapProvider.WallsNorthTilemap);
					}
					else
					{
						if (node.Direction.Value.y == 1)
						{
							SetWallTile(worldPosition + Vector3Int.left, passage, _tilemapProvider.WallsSouthTilemap, _tilemapProvider.WallsNorthTilemap);
							SetWallTile(worldPosition + Vector3Int.right * 2, passage, _tilemapProvider.WallsSouthTilemap, _tilemapProvider.WallsNorthTilemap);
						}
						else
						{
							SetWallTile(worldPosition + Vector3Int.left * 2, passage, _tilemapProvider.WallsSouthTilemap, _tilemapProvider.WallsNorthTilemap);
							SetWallTile(worldPosition + Vector3Int.right, passage, _tilemapProvider.WallsSouthTilemap, _tilemapProvider.WallsNorthTilemap);
						}
					}
				}
				else
				{
					// We are changing direction so we are on the corner. Place walls on the corner's outside
					//_tilemapProvider.WallsNorthTilemap.SetTile(worldPosition, _tileProvider.BlankGroundTile);
					//_tilemapProvider.WallsSouthTilemap.SetTile(worldPosition, _tileProvider.BlankGroundTile);

					Tilemap mainTilemap;
					Tilemap blankTilemap;
					if (previousNodeDirection.Value.y > 0)
					{
						mainTilemap = _tilemapProvider.WallsNorthTilemap;
						blankTilemap = _tilemapProvider.WallsSouthTilemap;
					}
					else
					{
						mainTilemap = _tilemapProvider.WallsSouthTilemap;
						blankTilemap = _tilemapProvider.WallsNorthTilemap;
					}

					PlaceTilesOnCorner(mainTilemap, blankTilemap, worldPosition, (Vector3Int) previousNodeDirection,
						(Vector3Int)newNodeDirection, passage);
				}

				previousNodeDirection = newNodeDirection;

				await Task.Delay((int)(pauseBetweenActions * 1000));
			}
		}

		private void SetGroundTile(Vector3Int position, Passage passage)
		{
			_tilemapProvider.GroundTilemap.SetTile(position, _tileProvider.GetGroundTile());
			_tilemapProvider.OverGroundTilemap.SetTile(position, _tileProvider.OverGroundTile);
			_tilemapProvider.WallsSouthTilemap.SetTile(position, _tileProvider.BlankGroundTile);
			_tilemapProvider.WallsNorthTilemap.SetTile(position, _tileProvider.BlankGroundTile);
			passage.AddTile(position, TileTypes.Ground);
		}

		private void SetWallTile(Vector3Int position, Passage passage, Tilemap wallTilemap, Tilemap blankWallTilemap)
		{
			if (_tilemapProvider.GroundTilemap.GetTile(position + Vector3Int.down) != null)
			{
				wallTilemap = _tilemapProvider.WallsNorthTilemap;
				blankWallTilemap = _tilemapProvider.WallsSouthTilemap;
			}
			else if (_tilemapProvider.GroundTilemap.GetTile(position + Vector3Int.up) != null)
			{
				wallTilemap = _tilemapProvider.WallsSouthTilemap;
				blankWallTilemap = _tilemapProvider.WallsNorthTilemap;
			}

			wallTilemap.SetTile(position, _tileProvider.GetWallTile());
			blankWallTilemap.SetTile(position, _tileProvider.BlankWallTile);
			_tilemapProvider.OverGroundTilemap.SetTile(position, _tileProvider.BlankWallTile);
			passage.AddTile(position, TileTypes.Wall);
		}

		/// <summary>
		/// W - placed wall
		/// P - previously placed wall
		/// | - up/down
		/// < - left
		/// 
		/// [ ][ ][P][P]		[W][W][P][P]
		/// [ ][ ][<][<]	->	[W][G][|][<]
		/// [ ][ ][<][<]		[W][G][|][<]
		/// [ ][ ][P][P]		[W][|][|][P]
		/// 
		/// 
		/// [ ][P][P][P]		[P][|][|][P]
		/// [ ][<][<][<]	->	[W][|][<][<]
		/// [ ][<][<][<]		[W][|][<][<]
		/// [ ][P][P][P]		[W][P][P][P]
		/// </summary>
		private void PlaceTilesOnCorner(Tilemap mainTilemap, Tilemap blankTilemap, Vector3Int worldPosition, Vector3Int previousDirection,
			Vector3Int newDirection, Passage passage)
		{
			bool isTurningLeft = (previousDirection.x * newDirection.y - previousDirection.y * newDirection.x) > 0;

			var firstWallPosition = worldPosition + previousDirection;
			if (isTurningLeft)
			{
				firstWallPosition += previousDirection;

				var firstGroundPosition = worldPosition + previousDirection - newDirection;
				SetGroundTile(firstGroundPosition, passage);
				SetGroundTile(firstGroundPosition - newDirection, passage);
			}

			SetWallTile(firstWallPosition, passage, mainTilemap, blankTilemap);
			SetWallTile(firstWallPosition - newDirection, passage, mainTilemap, blankTilemap);
			SetWallTile(firstWallPosition - newDirection * 2, passage, mainTilemap, blankTilemap);
			if (isTurningLeft)
			{
				SetWallTile(worldPosition - previousDirection, passage, blankTilemap, mainTilemap); // Inside corner wall
				SetWallTile(firstWallPosition - newDirection * 3, passage, mainTilemap, blankTilemap);

				// That's this case: https://app.clickup.com/t/86c4r411a
				if (newDirection.y < 0)
				{
					SetWallTile(firstWallPosition - newDirection * 3 - previousDirection, passage, blankTilemap, mainTilemap);
				}
				else
				{
					SetWallTile(firstWallPosition - newDirection * 3 - previousDirection, passage, mainTilemap, blankTilemap);
				}
			}
		}
	}
}