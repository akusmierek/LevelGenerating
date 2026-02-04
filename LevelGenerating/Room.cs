using Cornel.Enemies;
using Cornel.Minimap;
using Cornel.Utils;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cornel.LevelGenerating
{
	public class Room : MonoBehaviour
	{
		public static readonly int FREE_SPACE_SIZE = 3;

		[SerializeField, Required, AssetsOnly]
		protected EnemyProjectilesData _enemyProjectilesData = null;

		[Title("Doors")]
		[SerializeField, Required, AssetsOnly]
		private DoorSpawner _doorSpawner = null;
		public DoorSpawner DoorSpawner => _doorSpawner;

		[Title("Preview")]
		[SerializeField, Required]
		private SpriteRenderer _preview = null;
		public SpriteRenderer Preview => _preview;

		public int Id { get; set; } = 0;
		public RoomPreset Preset { get; set; } = null;
		public RoomType RoomType { get; set; } = RoomType.Normal;
		public RoomVisibilityStates Visibility { get; set; } = RoomVisibilityStates.NotSeen;

		private ItemSwappingPortal _itemSwappingPortal = null;
		public ItemSwappingPortal ItemSwappingPortal
		{
			get => _itemSwappingPortal;
			set
			{
				_itemSwappingPortal = value;
				InvokeRoomModified();
			}
		}

		private Vector2Int _position = Vector2Int.zero;
		public Vector2Int Position
		{
			get => _position;
			set
			{
				_position = value;
				transform.position = new Vector3(_position.x, _position.y);
			}
		}

		public Vector2 Center => _repositioning.Center;
		public Vector2Int Size => _repositioning.Size;
		public Vector2Int MinWithFreeSpace => _repositioning.MinWithFreeSpace;
		public Vector2Int MaxWithFreeSpace => _repositioning.MaxWithFreeSpace;

		[ShowInInspector]
		public List<RoomExit> Exits { get; } = new();
		public List<Passage> OutgoingPassages { get; } = new();
		public List<CollectableItem> SpawnedItems { get; } = new();
		public bool IsClosed { get; private set; } = false;
		public bool CanReposition { get; set; } = true;
		public AStarPathfinding Pathfinding { get; private set; } = null;
		public List<Door> Doors { get; private set; } = new();
		public int CurrentWave { get; private set; } = -1;

		public event Action<Room> Modified;
		public event Action<Room> Cleared;
		public event Action Opened;
		public event Action Closed;
		public event Action<float> IntensityChanged;

		private RoomRepositioning _repositioning = null;
		private RoomOnLevelPlacer _placer = null;
		private LevelController _levelController = null;
		protected readonly List<Enemy> _enemies = new();
		private readonly List<ItemSpawner> _loot = new();
		private EnemiesSetData _chosenEnemiesSet = null;
		private readonly List<IObjectInRoom> _objectsInRoom = new();
		private ITarget _currentTarget = null;
		private TilemapProvider _tilemapProvider = null;
		protected InRoomPrefabController _inRoomPrefabController = null;

		public void Initialize(LevelController levelController, TilemapProvider tilemapProvider, TileProvider tileProvider)
		{
			Pathfinding = new AStarPathfinding(Size.x, Size.y);
			Pathfinding.SetAllNodes(0);

			_levelController = levelController;
			_levelController.ActiveRoomChanged += OnActiveRoomChanged;

			_tilemapProvider = tilemapProvider;

			_preview.enabled = false;

			_placer.Place(tilemapProvider, tileProvider, OnEntered, OnExited, out _inRoomPrefabController);
			PlaceEnemies();
			
			foreach (var objectInRoom in _objectsInRoom)
			{
				objectInRoom.Initialize(this);
			}

			Pathfinding.AddNeighbors(canBeDiagonal: true);
		}

		public void Open()
		{
			SetRoomClosed(false);
			InvokeRoomModified();
		}

		public void Close()
		{
			SetRoomClosed(true);
			InvokeRoomModified();
		}

		public void AddObjectInRoom(IObjectInRoom objectInRoom)
		{
			_objectsInRoom.Add(objectInRoom);
		}
		
		public void AddSpawnedItem(CollectableItem item)
		{
			item.Collected += RemoveSpawnedItem;
			SpawnedItems.Add(item);
			InvokeRoomModified();
		}

		public void AddLoot(ItemSpawner itemSpawner)
		{
			_loot.Add(itemSpawner);
		}

		public void AddEnemy(Enemy enemy)
		{
			_enemies.Add(enemy);
		}

		public List<Enemy> GetEnemies(Enemy enemyToOmit = null)
		{
			if (enemyToOmit == null)
			{
				return _enemies;
			}

			return _enemies.Where(e => e != enemyToOmit).ToList();
		}

		public int GetEnemiesNoAlloc(Enemy[] enemies, Enemy enemyToOmit = null)
		{
			if (enemies == null || enemies.Length == 0)
			{
				Debug.LogError("Enemies array is null or has zero length!");
				return 0;
			}

			int count = 0;
			for (int i = 0; i < _enemies.Count; i++)
			{
				var enemy = _enemies[i];
				if (enemy != enemyToOmit)
				{
					enemies[count] = enemy;
					count++;
				}

				if (count >= enemies.Length)
				{
					return count;
				}
			}

			return count;
		}

		public void InitializeEnemyInRoom(Enemy enemy, bool instantSpawn = false)
		{
			InitializeEnemyInRoom(enemy, _currentTarget, instantSpawn);
		}

		public void InitializeEnemyInRoom(Enemy enemy, ITarget target, bool instantSpawn = false)
		{
			enemy.Initialize(target, this, instantSpawn);
			enemy.Died += RemoveEnemy;
		}

		public List<Node> GetPath(Vector2 from, Vector2 target)
		{
			return Pathfinding.CreatePath(from, target, Position, minimalizeTurns: true);
		}

		public RoomExit GetExit(RoomPreset.ExitPreset preset)
		{
			if (preset == null)
			{
				return null;
			}

			RoomExit exit = null;
			foreach (var existingExit in Exits)
			{
				if (existingExit.Preset == preset)
				{
					exit = existingExit;
					break;
				}
			}

			if (exit == null)
			{
				exit = new RoomExit(this, preset);
				Exits.Add(exit);
			}

			return exit;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="preferredExitDirection"></param>
		/// <param name="preferenceThreshold">-1 - 1 where -1 is opposite vector and 0 is perpendicular.</param>
		/// <returns>Selected exit or null when no exit matches preference threshold</returns>
		public RoomExit GetExitToNewRoom(Vector2? preferredExitDirection = null, float preferenceThreshold = -1f)
		{
			var possibleExitPresets = Preset.PossibleExits.ToList();

			// Remove already taken exits
			foreach (var existingExit in Exits)
			{
				for (int i = 0; i < possibleExitPresets.Count; i++)
				{
					if (possibleExitPresets[i] == existingExit.Preset)
					{
						possibleExitPresets.RemoveAt(i);
						i--;
					}
				}
			}

			// Sort by the best
			IOrderedEnumerable<RoomPreset.ExitPreset> possiblePresetsFromBest;

			if (preferredExitDirection.HasValue)
			{
				// Get exit that is closest to preferred direction
				possiblePresetsFromBest = possibleExitPresets
					.Where(preset => Vector2.Dot(preset.Direction, preferredExitDirection.Value) >= preferenceThreshold)
					.OrderByDescending(preset => Vector2.Dot(preset.Direction, preferredExitDirection.Value));
			}
			else
			{
				// Get exit that is the furthest from already taken exits
				Vector2 takenExitsCenter = Vector2.zero;
				foreach (var existingExit in Exits)
				{
					takenExitsCenter += existingExit.Preset.LeftPosition;
				}
				takenExitsCenter /= Exits.Count;

				possiblePresetsFromBest = possibleExitPresets.OrderByDescending(preset => (preset.LeftPosition - takenExitsCenter).sqrMagnitude);
			}

			int possiblePresetsCount = possiblePresetsFromBest.Count();
			List<float> weights = new();

			for (int i = possiblePresetsCount - 1; i >= 0; i--)
			{
				weights.Add(Mathf.Pow(i + 1, 2));
			}

			var chosenPreset = Utils.RandomUtils.GetRandomWeighted(possiblePresetsFromBest, weights);
			var exit = GetExit(chosenPreset);

			return exit;
		}

		public void SetPathfindingBlocker(int x, int y)
		{
			Pathfinding.SetNode(x, y, 1);
		}

		public void ForceEnter(Player.Player player)
		{
			_levelController.RoomEntered(this, player);
		}

		public void ForceExit(Player.Player player)
		{
			_levelController.RoomExited(this, player);
		}

		public void ChangeIntensity(float newIntensity)
		{
			IntensityChanged?.Invoke(newIntensity);
		}

		public Vector2 GetFreePositionInCenter()
		{
			var centerTile = new Vector2Int(Size.x / 2, Size.y / 2);
			var currentTile = centerTile;
			var radius = 0;

			while (true)
			{
				for (int x = -radius; x <= radius; x++)
				{
					// Check only borders of the square so we don't check insides multiple times
					if (x != -radius && x != radius)
					{
						continue;
					}

					for (int y = -radius; y <= radius; y++)
					{
						// Check only borders of the square so we don't check insides multiple times
						if (y != -radius && y != radius)
						{
							continue;
						}

						currentTile = centerTile + new Vector2Int(x, y);
						var node = Pathfinding.GetNode(currentTile.x, currentTile.y);
						if (node != null && node.Height == 0)
						{
							node.SetWorldPosition(Position);
							Vector3Int worldPositionInt = new((int)node.WorldPosition.x, (int)node.WorldPosition.y);
							if (_tilemapProvider.GroundTilemap.HasTile(worldPositionInt))
							{
								return node.WorldPosition;
							}
						}
					}
				}

				radius++;
			}
		}

		#region Repositioning

		public Vector2Int GetRepositionVector(Vector2Int otherMin, Vector2Int otherMax)
		{
			return _repositioning.GetRepositionVector(otherMin, otherMax);
		}

		public bool OverlapsWith(Room otherRoom)
		{
			return _repositioning.OverlapsWith(otherRoom);
		}

		public void Reposition(Vector2Int otherRoomMin, Vector2Int otherRoomMax, List<Room> connectedRoomsToReposition)
		{
			_repositioning.Reposition(otherRoomMin, otherRoomMax, connectedRoomsToReposition);
		}

		public void RelaxExits(List<Room> connectedRoomsToReposition)
		{
			_repositioning.RelaxExits(connectedRoomsToReposition);
		}

		#endregion

		#region Unity methods

		private void Awake()
		{
			_repositioning = new(this);
			_placer = new(this);
		}

		private void OnDestroy()
		{
			StopAllCoroutines();

			foreach (var door in Doors)
			{
				if (door != null)
				{
					if (door.EnterTrigger != null)
					{
						door.EnterTrigger.Entered -= OnEntered;
					}

					if (door.ExitTrigger != null)
					{
						door.ExitTrigger.Entered -= OnExited;
					}
				}
			}

			if (_levelController != null)
			{
				_levelController.ActiveRoomChanged -= OnActiveRoomChanged;
			}
		}

		#endregion

		#region Private methods

		private void OnEntered(Collider2D collider)
		{
			var rigidbody = collider.attachedRigidbody;
			if (rigidbody == null || !rigidbody.TryGetComponent(out Player.Player player))
			{
				return;
			}

			_levelController.RoomEntered(this, player);
		}

		private void OnExited(Collider2D collider)
		{
			var rigidbody = collider.attachedRigidbody;
			if (rigidbody == null || !rigidbody.TryGetComponent(out Player.Player player))
			{
				return;
			}

			_levelController.RoomExited(this, player);
		}

		private void OnActiveRoomChanged(Room activeRoom, Player.Player player)
		{
			if (activeRoom == null)
			{
				return;
			}

			if (activeRoom != this)
			{
				if (Visibility == RoomVisibilityStates.NotSeen && AreRoomsNeighbors(this, activeRoom))
				{
					Visibility = RoomVisibilityStates.NotVisited;
				}
				else if (Visibility == RoomVisibilityStates.Current)
				{
					Visibility = RoomVisibilityStates.Visited;
				}

				InvokeRoomModified();

				return;
			}

			Visibility = RoomVisibilityStates.Current;

			SetRoomAsActive(player);

			InvokeRoomModified();
		}

		private bool AreRoomsNeighbors(Room roomA, Room roomB)
		{
			foreach (var exit in roomA.Exits)
			{
				if (exit.IncomingExits.Any(incomingExit => incomingExit.Room == roomB) || exit.OutgoingExits.Any(outgoingExit => outgoingExit.Room == roomB))
				{
					return true;
				}
			}

			return false;
		}

		protected virtual void SetRoomAsActive(Player.Player player)
		{
			_currentTarget = player;

			if (_enemies.Any(e => e.CanActivate))
			{
				SetRoomClosed(true);

				foreach (var enemy in _enemies)
				{
					InitializeEnemyInRoom(enemy, _currentTarget);
				}
			}
			else
			{
				SetRoomClosed(false);
			}
		}

		protected void SetRoomClosed(bool setClosed)
		{
			IsClosed = setClosed;

			if (setClosed)
			{
				Closed?.Invoke();
			}
			else
			{
				Opened?.Invoke();
			}
		}

		protected virtual void RemoveEnemy(Enemy enemy)
		{
			_enemies.Remove(enemy);

			if (_enemies.Count == 1)
			{
				SpawnNextWave();
			}

			if (_enemies.Count <= 0)
			{
				OnRoomCleared();
			}
		}

		protected virtual void OnRoomCleared()
		{
			SetRoomClosed(false);

			Cleared?.Invoke(this);

			foreach (var itemSpawner in _loot)
			{
				itemSpawner.SpawnRandomItemNearPlayer();
			}

			foreach (var spawnedItem in SpawnedItems)
			{
				spawnedItem.OnRoomCleared();
			}

			_enemyProjectilesData.DeactivateAll();

			InvokeRoomModified();
		}

		private void RemoveSpawnedItem(CollectableItem item)
		{
			SpawnedItems.Remove(item);

			InvokeRoomModified();
		}

		protected void InvokeRoomModified()
		{
			Modified?.Invoke(this);
		}

		private void PlaceEnemies()
		{
			if (Preset.EnemiesSetsData == null || Preset.EnemiesSetsData.Length == 0)
			{
				return;
			}

			_chosenEnemiesSet = Preset.EnemiesSetsData[RandomUtils.NextInt(0, Preset.EnemiesSetsData.Length)];

			SpawnNextWave();
		}

		private void SpawnNextWave()
		{
			CurrentWave++;
			_chosenEnemiesSet.SpawnWave(this);
		}

		#endregion
	}
}