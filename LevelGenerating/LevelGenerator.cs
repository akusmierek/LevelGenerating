using Cornel.Hub;
using Cornel.Player;
using Cornel.Utils;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Cornel.LevelGenerating
{
	public class LevelGenerator : MonoBehaviour
	{
		[SerializeField, Required, AssetsOnly]
		private GameObject _roomPrefab = null;

		[SerializeField, Required, AssetsOnly]
		private GameObject _bossRoomPrefab = null;

		[SerializeField, Required]
		private Transform _roomsParent = null;

		[SerializeField, Required, AssetsOnly]
		private PlayerData _playerData = null;

		[SerializeField, Required, AssetsOnly]
		private LevelData _levelData = null;

		[SerializeField]
		private bool _testLayout = false;

		[SerializeField, ShowIf(nameof(_testLayout))]
		private LevelLayout[] _possibleLayouts = null;

		[SerializeField, ShowIf(nameof(_testLayout))]
		private LevelType _testLevelType = null;

		[SerializeField, Required]
		private PassagesGenerator _passagesGenerator = null;

		[SerializeField]
		private bool _forceMatchExitDirections = false;

		[Title("Tilemaps")]
		[SerializeField, Required]
		private TilemapProvider _tilemapProvider = null;
		public TilemapProvider TilemapProvider => _tilemapProvider;

		[SerializeField]
		private Tilemap _wallsNorthTilemap = null;

		[SerializeField]
		private Tilemap _wallsSouthTilemap = null;

		[SerializeField]
		private Tilemap _groundTilemap = null;

		[SerializeField]
		private Tilemap _overGroundTilemap = null;

		[Title("Hidden Room")]
		[SerializeField, Required, AssetsOnly]
		private ProgressData _progressData = null;

		[SerializeField, Required, AssetsOnly]
		private GameUpgradeData _hiddenRoomGameUpgrade = null;

		[SerializeField, MinValue(0)]
		private int _hiddenRoomOffset = 16;

		[Title("Visualisation")]
		[SerializeField, MinValue(0f), Unit(Units.Second)]
		private float _pauseBetweenActions = 0.1f;

		[SerializeField]
		private bool _stopOnFail = false;

		[SerializeField]
		private bool _randomizeSeed = true;

		[SerializeField, HideIf(nameof(_randomizeSeed))]
		private int _seed = 0;

		[SerializeField]
		private bool _generatePassages = true;

		[Title("Debug")]
		[SerializeField]
		private bool _continuousGeneration = false;

		public static event Action LevelGenerated;
		
		public TileProvider TileProvider => _levelData.CurrentLevelSettings.TileProvider;
		public bool IsGenerating { get; private set; } = false;

		private readonly Dictionary<uint, Room> _idsToPlacedRooms = new();
		public Dictionary<uint, Room> IdsToPlacedRooms => _idsToPlacedRooms;

		private LevelController _levelController = null;
		private LevelLayout _currentLayout = null;
		private static int _roomNumber = 0;
		private bool _isLevelGenerated = false;
		private readonly List<GeneratedGroup> _generatedGroups = new();
		private readonly HashSet<RoomPreset> _usedRoomPresets = new();
		private const int HIDDEN_ROOM_NUMBER = 9; // 9

		public void GenerateLevel(LevelController levelController)
		{
			IsGenerating = true;

			_tilemapProvider.WallsNorthTilemap = _wallsNorthTilemap;
			_tilemapProvider.WallsSouthTilemap = _wallsSouthTilemap;
			_tilemapProvider.GroundTilemap = _groundTilemap;
			_tilemapProvider.OverGroundTilemap = _overGroundTilemap;

			_levelController = levelController;

			_isLevelGenerated = false;

			_passagesGenerator.Initialize(_tilemapProvider, _levelData.CurrentLevelSettings.TileProvider, _levelController);

			GenerateLevelAsync();
		}

		private async void GenerateLevelAsync()
		{
			while (!_isLevelGenerated)
			{
				_isLevelGenerated = false;

				if (_randomizeSeed)
				{
					_seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
				}

				RandomUtils.InitRNG(_seed);

				try
				{
					await GenerateLevelInternal();
				}
				catch (OperationCanceledException e)
				{
					Debug.Log(e.Message);
					Debug.Log("Generating level failed - restarting");
				}
				finally
				{
					if (!_isLevelGenerated)
					{
						_randomizeSeed = true;
					}
				}

				if (!_isLevelGenerated)
				{
					if (_stopOnFail)
					{
						IsGenerating = false;
						break;
					}
				}
			}
		}

		private async Task GenerateLevelInternal()
		{
			ClearPreviousLevel();

			// Make sure that everything gets cleaned
			await Task.Yield();

			SelectLayout();

			await CreateGroups();

			await ConnectAndRepositionGroups();

			await CreateAdditionalRooms();

			await PlaceRoomsTiles();

			if (_generatePassages)
			{
				var passagesHolder = new GameObject("PassagesHolder").transform;
				passagesHolder.parent = _roomsParent;

				bool arePassagesCreated = await _passagesGenerator.TryCreatePassagesBetweenRooms(passagesHolder, _idsToPlacedRooms, _pauseBetweenActions);

				if (!arePassagesCreated)
				{
					throw new OperationCanceledException("Couldn't create passages!");
				}
			}

			CheckWallsIntegrity();

			AddLootToRooms();
			AddIdsToRooms();
			AddHiddenRoom();

			if (_idsToPlacedRooms.Count > 0)
			{
				while (_playerData.Player == null)
				{
					await Task.Yield();
				}

				_playerData.Player.transform.position = _idsToPlacedRooms[0].Center;
			}

			LevelGenerated?.Invoke();
			_isLevelGenerated = _continuousGeneration ? false : true;
			IsGenerating = false;
		}

		private void ClearPreviousLevel()
		{
			_wallsNorthTilemap.ClearAllTiles();
			_wallsSouthTilemap.ClearAllTiles();
			_groundTilemap.ClearAllTiles();
			_overGroundTilemap.ClearAllTiles();
			_generatedGroups.Clear();
			_usedRoomPresets.Clear();

			for (int i = _roomsParent.childCount - 1; i >= 0; i--)
			{
				Destroy(_roomsParent.GetChild(i).gameObject);
			}

			_idsToPlacedRooms.Clear();
			_roomNumber = 0;
		}

		private void SelectLayout()
		{
			if (_testLayout)
			{
				_currentLayout = _possibleLayouts[RandomUtils.NextInt(0, _possibleLayouts.Length)];
				_levelData.CurrentLevelType = _testLevelType;
			}
			else
			{
				if (_levelData.CurrentLevelSettings.IsFirstLevel)
				{
					var possibleFirstLevelTypes = _levelData.CurrentLevelSettings.PossibleFirstLevelTypes;
					_levelData.CurrentLevelType = possibleFirstLevelTypes[Utils.RandomUtils.NextInt(0, possibleFirstLevelTypes.Length)];
				}

				if (_levelData.CurrentLevelType == null)
				{
					var possibleLevelTypes = _levelData.CurrentLevelSettings.PossibleNextLevelTypes;
					_levelData.CurrentLevelType = possibleLevelTypes[Utils.RandomUtils.NextInt(0, possibleLevelTypes.Length)];
				}

				var possibleLayouts = _levelData.CurrentLevelType.PossibleLayouts;
				_currentLayout = possibleLayouts[RandomUtils.NextInt(0, possibleLayouts.Length)];
			}
		}

		private async Task CreateGroups()
		{
			for (int i = 0; i < _currentLayout.Groups.Length; i++)
			{
				var group = _currentLayout.Groups[i];
				GeneratedGroup generatedGroup;

				if (group.IsCycle)
				{
					generatedGroup = await GenerateCycle(group);
				}
				else
				{
					generatedGroup = await CreateRoomsInGroup(group.Nodes);
				}

				generatedGroup.LayoutGroup = group;
				generatedGroup.LockRepositioning();
				generatedGroup.SetVisible(false);

				_generatedGroups.Add(generatedGroup);
			}
		}

		private async Task ConnectAndRepositionGroups()
		{
			for (int i = 0; i < _generatedGroups.Count; i++)
			{
				GeneratedGroup group = _generatedGroups[i];
				group.SetVisible(true);

				foreach (var roomNode in group.LayoutGroup.Nodes)
				{
					var room = _idsToPlacedRooms[roomNode.Id];
					bool shouldConnectToAnotherGroup = group.LayoutGroup.Nodes.All(node => node.Id != roomNode.NodeIdToConnectTo);
					if (!shouldConnectToAnotherGroup)
					{
						continue;
					}

					if (!TryGetConnectedGroupAndRoom(roomNode.NodeIdToConnectTo, out var connectedGroup, out var connectedRoom))
					{
						throw new OperationCanceledException($"Didn't find node id {roomNode.NodeIdToConnectTo} in any group!");
					}

					RoomExit roomExit;
					RoomExit connectedRoomExit;
					if (group.Rooms.Count >= connectedGroup.Rooms.Count)
					{
						roomExit = group.GetExitAimingOutsideGroup(room);
						if (roomExit == null)
						{
							throw new OperationCanceledException("Couldn't get exit to connect groups!");
						}

						connectedRoomExit = connectedRoom.GetExitToNewRoom(-roomExit.Preset.Direction, 0.5f);
						if (connectedRoomExit == null)
						{
							throw new OperationCanceledException("Couldn't get one more exit for room to connect groups!");
						}
					}
					else
					{
						connectedRoomExit = connectedGroup.GetExitAimingOutsideGroup(connectedRoom);
						if (connectedRoomExit == null)
						{
							throw new OperationCanceledException("Couldn't get exit to connect groups!");
						}

						roomExit = room.GetExitToNewRoom(-connectedRoomExit.Preset.Direction, 0.5f);
						if (roomExit == null)
						{
							throw new OperationCanceledException("Couldn't get one more exit for room to connect groups!");
						}
					}

					roomExit.IncomingExits.Add(connectedRoomExit);
					connectedRoomExit.OutgoingExits.Add(roomExit);

					// Move the whole group
					var targetExitPosition = connectedRoomExit.RightWorldPosition + connectedRoomExit.Preset.Direction * Room.FREE_SPACE_SIZE -
						roomExit.Preset.Direction * Room.FREE_SPACE_SIZE;

					var offset = targetExitPosition - roomExit.LeftWorldPosition;
					group.Move(offset);

					// Block moving exits too close to each other
					Vector2Int blockedDirection = roomExit.Preset.Direction - connectedRoomExit.Preset.Direction;
					if (Mathf.Abs(blockedDirection.x) > 0)
					{
						blockedDirection.x = (int)Mathf.Sign(blockedDirection.x);
					}
					if (Mathf.Abs(blockedDirection.y) > 0)
					{
						blockedDirection.y = (int)Mathf.Sign(blockedDirection.y);
					}

					await RepositionGroup(i, blockedDirection);

					await Task.Delay((int)(_pauseBetweenActions * 1000));
				}
			}
		}

		private async Task RepositionGroup(int groupId, Vector2Int blockedDirection)
		{
			var group = _generatedGroups[groupId];
			bool repositionPreviousGroups = false;
			int maxTries = 20;
			int tries = 0;
			for (int i = 0; i < groupId; i++)
			{
				var otherGroup = _generatedGroups[i];
				Vector2Int lastRepositionVector = Vector2Int.zero;
				while (group.OverlapsWith(otherGroup, out var overlappingRoom, out var otherOverlappingRoom))
				{
					repositionPreviousGroups = true;

					Utils2D.GetRepositionVectors(overlappingRoom.MinWithFreeSpace, overlappingRoom.MaxWithFreeSpace,
						otherOverlappingRoom.MinWithFreeSpace, otherOverlappingRoom.MaxWithFreeSpace,
						out var overlapVector, out var moveDirection);

					if (overlapVector == Vector2Int.zero)
					{
						continue;
					}

					bool forceMoveX = false;
					bool forceMoveY = false;

					if (moveDirection.x == blockedDirection.x && moveDirection.y == blockedDirection.y)
					{
						moveDirection = -moveDirection;
					}
					else if (moveDirection.x == blockedDirection.x)
					{
						forceMoveY = true;
					}
					else if (moveDirection.y == blockedDirection.y)
					{
						forceMoveX = true;
					}

					// Move only in one axis at once
					Vector2Int moveAwayVector = Vector2Int.one;
					if (forceMoveX)
					{
						moveAwayVector.y = 0;
					}
					else if (forceMoveY)
					{
						moveAwayVector.x = 0;
					}
					else
					{
						if (overlapVector.x < overlapVector.y)
						{
							moveAwayVector.y = 0;
						}
						if (overlapVector.y < overlapVector.x)
						{
							moveAwayVector.x = 0;
						}
					}

					var repositionVector = moveAwayVector * moveDirection;
					if (repositionVector == -lastRepositionVector)
					{
						// We entered the loop of trying to move back and forth so there's no space for a room.
						throw new OperationCanceledException("Couldn't reposition groups so they don't collide!");
					}

					lastRepositionVector = repositionVector;
					group.Move(repositionVector);

					await Task.Delay((int)(_pauseBetweenActions * 1000));
				}

				// TODO: It may not be the best solution as sometimes there is space
				// to place the group but the search for it is just not the best
				if (repositionPreviousGroups)
				{
					repositionPreviousGroups = false;
					if (tries >= maxTries)
					{
						throw new OperationCanceledException("Couldn't reposition groups so they don't collide!");
					}

					tries++;
					i = 0;
				}
			}
		}

		private bool TryGetConnectedGroupAndRoom(uint nodeId, out GeneratedGroup connectedGroup, out Room connectedRoom)
		{
			foreach (var group in _generatedGroups)
			{
				foreach (var roomNode in group.LayoutGroup.Nodes)
				{
					if (roomNode.Id == nodeId)
					{
						connectedGroup = group;
						connectedRoom = _idsToPlacedRooms[nodeId];
						return true;
					}
				}
			}

			connectedGroup = null;
			connectedRoom = null;
			return false;
		}

		private async Task<GeneratedGroup> CreateRoomsInGroup(RoomNode[] nodes)
		{
			GeneratedGroup generatedGroup = new();
			for (int i = 0; i < nodes.Length; i++)
			{
				var node = nodes[i];

				if (i == 0)
				{
					CreateFirstRoomInGroup(node, generatedGroup);
					continue;
				}

				if (!_idsToPlacedRooms.TryGetValue(node.NodeIdToConnectTo, out var roomToConnectTo))
				{
					throw new OperationCanceledException($"Couldn't connect roomId {node.Id} to roomId {node.NodeIdToConnectTo}. Probably it wasn't created yet.");
				}

				if (_idsToPlacedRooms.ContainsKey(node.Id))
				{
					Debug.LogWarning($"Tried to place roomId {node.Id} that already exists");

					continue;
				}

				Vector2? preferredExitDirection = node.RoomType == RoomType.Boss ? Vector2.up : null;
				float preferenceThreshold = node.RoomType == RoomType.Boss ? 0.5f : -1;

				var roomToConnectToExit = roomToConnectTo.GetExitToNewRoom(preferredExitDirection, preferenceThreshold);

				if (roomToConnectToExit == null)
				{
					throw new OperationCanceledException("Couldn't get exit to new room");
				}

				if (node.RoomType == RoomType.Boss)
				{
					roomToConnectToExit.Type = RoomExit.Types.ToBossRoom;
				}
				else if (node.RoomType == RoomType.Shop)
				{
					roomToConnectToExit.Type = RoomExit.Types.ToShopRoom;
				}
				
				var createdRoom = await TryCreateRoom(node, roomToConnectToExit, generatedGroup);
				if (createdRoom == null)
				{
					throw new OperationCanceledException("Couldn't create a room in group!");
				}
			}

			return generatedGroup;
		}

		private Room CreateFirstRoomInGroup(RoomNode roomNode, GeneratedGroup generatedGroup)
		{
			var selectedPreset = _levelData.CurrentLevelType.RoomTypeToPresetsHolder.GetUniquePreset(roomNode.RoomType, _usedRoomPresets);
			_usedRoomPresets.Add(selectedPreset);
			var createdRoom = GetNewRoom(selectedPreset, roomNode.RoomType, roomNode.Id, Vector2Int.zero);
			generatedGroup.Rooms.Add(createdRoom);
			return createdRoom;
		}

		private async Task CreateAdditionalRooms()
		{
			foreach (var additionalRoom in _currentLayout.AdditionalRooms)
			{
				if (!additionalRoom.CanPlace())
				{
					continue;
				}

				var possibleRooms = _idsToPlacedRooms.Values.Where(placedRoom => CanConnectAdditionalRoomTo(additionalRoom, placedRoom));
				if (possibleRooms.Count() <= 0)
				{
					continue;
				}

				Room createdRoom = null;
				const int maxTries = 20;
				for (int i = 0; i < maxTries; i++)
				{
					var roomToConnectTo = possibleRooms.ElementAt(RandomUtils.NextInt(0, possibleRooms.Count()));
					uint nodeId = _idsToPlacedRooms.Keys.Max() + 1;
					var nextNode = new RoomNode(nodeId, additionalRoom.RoomType);
					var previousRoomExit = roomToConnectTo.GetExitToNewRoom();
					if (previousRoomExit == null)
					{
						throw new OperationCanceledException("Couldn't get exit to new room");
					}

					createdRoom = await TryCreateRoom(nextNode, previousRoomExit);
					if (createdRoom != null)
					{
						break;
					}
					else
					{
						roomToConnectTo.Exits.Remove(previousRoomExit);
					}
				}

				if (createdRoom == null)
				{
					throw new OperationCanceledException($"Couldn't create additional room.");
				}
			}
		}

		private bool CanConnectAdditionalRoomTo(LevelLayout.AdditionalRoom additionalRoom, Room room)
		{
			return additionalRoom.PossibleConnections.HasFlag(room.RoomType);
		}

		private async Task PlaceRoomsTiles()
		{
			foreach (var room in _idsToPlacedRooms.Values)
			{
				room.Initialize(_levelController, _tilemapProvider, _levelData.CurrentLevelSettings.TileProvider);
				await Task.Delay((int)(_pauseBetweenActions * 1000));
			}
		}

		private async Task<Room> TryCreateRoom(RoomNode roomNode, RoomExit previousRoomExit, GeneratedGroup generatedGroup = null)
		{
			int maxTries = 10;

			for (int i = 0; i < maxTries; i++)
			{
				var selectedPreset = GetRoomPreset(roomNode, previousRoomExit.Preset, out var selectedEntrance);
				if (selectedPreset == null)
				{
					throw new OperationCanceledException("Couldn't select preset");
				}

				var newRoomPosition = previousRoomExit.LeftWorldPosition +
					previousRoomExit.Preset.Direction * Room.FREE_SPACE_SIZE -
					selectedEntrance.Direction * Room.FREE_SPACE_SIZE -
					selectedEntrance.LeftPosition;

				var newRoom = GetNewRoom(selectedPreset, roomNode.RoomType, roomNode.Id, newRoomPosition);
				if (generatedGroup != null)
				{
					generatedGroup.Rooms.Add(newRoom);
				}
				var newRoomExit = newRoom.GetExit(selectedEntrance);
				previousRoomExit.OutgoingExits.Add(newRoomExit);
				newRoomExit.IncomingExits.Add(previousRoomExit);

				bool couldRepositionRoom;
				couldRepositionRoom = await TryRepositionRoom(newRoom, generatedGroup);

				if (!couldRepositionRoom)
				{
					Debug.LogWarning("Couldn't reposition room - deleting room and trying to create new one");
					// Delete new room
					Destroy(newRoom.gameObject);
					_idsToPlacedRooms.Remove(roomNode.Id);
					_usedRoomPresets.Remove(selectedPreset);
					if (generatedGroup != null)
					{
						generatedGroup.Rooms.RemoveAt(generatedGroup.Rooms.Count - 1);
					}
					previousRoomExit.OutgoingExits.RemoveAt(previousRoomExit.OutgoingExits.Count - 1);
					continue;
				}

				return newRoom;
			}

			return null;
		}

		private Room GetNewRoom(RoomPreset preset, RoomType roomType, uint nodeId, Vector2Int position)
		{
			var roomPrefab = roomType == RoomType.Boss ? _bossRoomPrefab : _roomPrefab;
			var newRoom = Instantiate(roomPrefab, (Vector2)position, Quaternion.identity, _roomsParent).GetComponent<Room>();

			newRoom.gameObject.name += _roomNumber.ToString();
			_roomNumber++;

			newRoom.Position = position;
			newRoom.Preset = preset;
			newRoom.RoomType = roomType;
			newRoom.Preview.enabled = true;
			newRoom.Preview.size = preset.Size;

			_idsToPlacedRooms.Add(nodeId, newRoom);

			return newRoom;
		}

		private RoomPreset GetRoomPreset(RoomNode roomNode, RoomPreset.ExitPreset previousRoomExit, out RoomPreset.ExitPreset selectedEntrance)
		{
			RoomPreset selectedRoomPreset;
			selectedEntrance = null;

			const int maxTries = 10;

			for (int i = 0; i < maxTries; i++)
			{
				selectedRoomPreset = _levelData.CurrentLevelType.RoomTypeToPresetsHolder.GetUniquePreset(roomNode.RoomType, _usedRoomPresets);

				if (selectedRoomPreset.TryGetExitPreset(previousRoomExit.Direction, out selectedEntrance, _forceMatchExitDirections))
				{
					_usedRoomPresets.Add(selectedRoomPreset);
					return selectedRoomPreset;
				}
			}

			selectedRoomPreset = null;
			return selectedRoomPreset;
		}

		private async Task<bool> TryRepositionRoom(Room movedRoom, GeneratedGroup generatedGroup = null, bool relaxExits = false)
		{
			List<Room> connectedRoomsToReposition = new();
			bool couldRepositionConnectedRooms = true;

			if (relaxExits)
			{
				movedRoom.RelaxExits(connectedRoomsToReposition);
				await Task.Delay((int)(_pauseBetweenActions * 1000));
			}

			const int maxTries = 100; // 20?

			for (int i = 0; i < maxTries; i++)
			{
				var overlappingRooms = GetOverlappingRooms(movedRoom, generatedGroup);
				var blockedOverlappingRooms = overlappingRooms.Where(room => !room.CanReposition);
				if (blockedOverlappingRooms.Count() >= 2)
				{
					// It will be hard to place a room here - try different place
					return false;
				}

				if (overlappingRooms.Count == 0)
				{
					// If nothing overlaps we still want to continue relaxing exits
					couldRepositionConnectedRooms = await TryRepositionConnectedRooms(connectedRoomsToReposition, generatedGroup);
					if (!couldRepositionConnectedRooms)
					{
						return false;
					}

					connectedRoomsToReposition.Clear();

					return true;
				}

				var overlappingRoom = overlappingRooms.FirstOrDefault(room => room.CanReposition);

				if (overlappingRoom == null)
				{
					overlappingRoom = blockedOverlappingRooms.FirstOrDefault();
				}

				if (overlappingRoom.CanReposition) // if generatedGroup == null?
				{
					overlappingRoom.Reposition(movedRoom.MinWithFreeSpace, movedRoom.MaxWithFreeSpace, connectedRoomsToReposition);
				}

				movedRoom.Reposition(overlappingRoom.MinWithFreeSpace, overlappingRoom.MaxWithFreeSpace, connectedRoomsToReposition);

				await Task.Delay((int)(_pauseBetweenActions * 1000));

				couldRepositionConnectedRooms = await TryRepositionConnectedRooms(connectedRoomsToReposition, generatedGroup);
				if (!couldRepositionConnectedRooms)
				{
					return false;
				}

				if (overlappingRoom.CanReposition) // if generatedGroup == null?
				{
					bool repositionedRoom = await TryRepositionRoom(overlappingRoom, generatedGroup);
					if (!repositionedRoom)
					{
						return false;
					}
				}

				connectedRoomsToReposition.Clear();
			}

			throw new OperationCanceledException("Tried reposition overlapping rooms too many times");
		}

		private async Task<bool> TryRepositionConnectedRooms(List<Room> rooms, GeneratedGroup generatedGroup = null)
		{
			foreach (var room in rooms)
			{
				if (!room.CanReposition)
				{
					continue;
				}

				bool couldRepositionRoom = await TryRepositionRoom(room, generatedGroup, relaxExits: true);
				if (!couldRepositionRoom)
				{
					return false;
				}
			}

			return true;
		}

		private List<Room> GetOverlappingRooms(Room movedRoom, GeneratedGroup generatedGroup = null)
		{
			var overlappingRooms = new List<Room>();
			var rooms = generatedGroup == null ? _idsToPlacedRooms.Values.ToList() : generatedGroup.Rooms;
			foreach (var room in rooms)
			{
				if (room.OverlapsWith(movedRoom))
				{
					overlappingRooms.Add(room);
				}
			}

			return overlappingRooms;
		}

		private async Task<GeneratedGroup> GenerateCycle(LevelLayout.Group group)
		{
			RoomNode[] rooms = group.Nodes;
			GeneratedGroup generatedGroup = new();
			Room firstRoom = null;
			Room previousRoom = null;
			RoomExit previousRoomExit = null;
			Vector2? preferredExitDirection = null;
			Vector2? cycleDirection = null;

			for (int i = 0; i < rooms.Length; i++)
			{
				RoomNode roomNode = rooms[i];

				if (i == 0)
				{
					firstRoom = previousRoom = CreateFirstRoomInGroup(roomNode, generatedGroup);
					continue;
				}

				if (i > 1)
				{
					if (i <= rooms.Length / 2)
					{
						preferredExitDirection = cycleDirection.Value;
					}
					else
					{
						preferredExitDirection = (firstRoom.Center - previousRoomExit.LeftWorldPosition).normalized;
					}
				}

				previousRoomExit = previousRoom.GetExitToNewRoom(preferredExitDirection, 0.1f);
				if (previousRoomExit == null)
				{
					throw new OperationCanceledException("Couldn't find proper exit to connect rooms in a cycle");
				}

				if (group.IsClosed && i == 0)
				{
					previousRoomExit.Type = RoomExit.Types.ToClosedCycle;
				}

				if (!cycleDirection.HasValue)
				{
					cycleDirection = previousRoomExit.Preset.Direction;
				}

				var createdRoom = await TryCreateRoom(roomNode, previousRoomExit, generatedGroup);
				if (createdRoom == null)
				{
					throw new OperationCanceledException("Couldn't create a room in a cycle!");
				}

				previousRoom = _idsToPlacedRooms[roomNode.Id];
			}

			// Connect last rooms
			preferredExitDirection = (firstRoom.Center - previousRoom.Center).normalized;
			previousRoomExit = previousRoom.GetExitToNewRoom(preferredExitDirection, 0f);
			if (previousRoomExit == null)
			{
				throw new OperationCanceledException("Couldn't connect first and last room in a cycle - A");
			}

			preferredExitDirection = ((Vector2)previousRoomExit.LeftWorldPosition - firstRoom.Center).normalized;
			var firstRoomEntrance = firstRoom.GetExitToNewRoom(preferredExitDirection, 0f);
			if (firstRoomEntrance == null)
			{
				throw new OperationCanceledException("Couldn't connect first and last room in a cycle - B");
			}

			if (group.IsClosed)
			{
				firstRoomEntrance.Type = RoomExit.Types.ToClosedRoom;
			}

			previousRoomExit.OutgoingExits.Add(firstRoomEntrance);
			firstRoomEntrance.IncomingExits.Add(previousRoomExit);

			// Final exits could be in the same direction...
			if (previousRoomExit.Preset.Direction == firstRoomEntrance.Preset.Direction)
			{
				throw new OperationCanceledException("Tried to relax exits that were in the same direction. Restarting to avoid infinite loop.");
			}
			
			// Relax final exits
			await TryRepositionRoom(previousRoom, generatedGroup, relaxExits: true);
			
			return generatedGroup;
		}

		private void CheckWallsIntegrity()
		{
			int minX = int.MaxValue;
			int minY = int.MaxValue;
			int maxX = int.MinValue;
			int maxY = int.MinValue;

			foreach (var room in _idsToPlacedRooms.Values)
			{
				minX = Mathf.Min(minX, room.MinWithFreeSpace.x);
				minY = Mathf.Min(minY, room.MinWithFreeSpace.y);
				maxX = Mathf.Max(maxX, room.MaxWithFreeSpace.x);
				maxY = Mathf.Max(maxY, room.MaxWithFreeSpace.y);
			}

			Vector3Int position = new(0, 0);
			Vector3Int position2 = new(0, 0);
			for (int x = minX; x <= maxX; x++)
			{
				position.x = x;
				for (int y = minY; y <= maxY; y++)
				{
					position.y = y;
					if (_wallsSouthTilemap.GetTile(position) is not WallRuleTile && _wallsNorthTilemap.GetTile(position) is not WallRuleTile)
					{
						continue;
					}

					// There's a wall. Check if it has at least 2 connected walls.
					int connectedWalls = 0;
					for (int x2 = x - 1; x2 <= x + 1; x2++)
					{
						position2.x = x2;
						for (int y2 = y - 1; y2 <= y + 1; y2++)
						{
							position2.y = y2;

							if (x2 == x && y2 == y)
							{
								// Don't count itself
								continue;
							}

							if (_wallsSouthTilemap.GetTile(position2) is WallRuleTile && _wallsNorthTilemap.GetTile(position2) == TileProvider.BlankWallTile ||
								_wallsNorthTilemap.GetTile(position2) is WallRuleTile && _wallsSouthTilemap.GetTile(position2) == TileProvider.BlankWallTile)
							{
								connectedWalls++;
							}
						}
					}

					if (connectedWalls < 2)
					{
						throw new OperationCanceledException("For some reason the walls weren't connected!");
					}
				}
			}
		}

		private void AddLootToRooms()
		{
			var possibleRooms = _idsToPlacedRooms.Values.Where(placedRoom => CanHaveLoot(placedRoom)).ToList();
			var possibleRoomsCount = possibleRooms.Count;

			for (int i = 0; i < possibleRoomsCount; i++)
			{
				var room = possibleRooms[i];
				var loot = _levelData.CurrentLevelType.GetRandomCombatRoomLootSpawner();
				room.AddLoot(loot);
			}
		}

		private void AddIdsToRooms()
		{
			foreach (var room in _idsToPlacedRooms.Values)
			{
				room.Id = _levelData.LastRoomId + 1;
				_levelData.LastRoomId++;
			}
		}

		private void AddHiddenRoom()
		{
			if (!_progressData.IsGameUpgradeUnlocked(_hiddenRoomGameUpgrade))
			{
				return;
			}

			Passage possibleIncomingPassage = null;
			bool didSetHiddenRoom = false;
			foreach (var room in _idsToPlacedRooms.Values)
			{
				if (room.Id != HIDDEN_ROOM_NUMBER)
				{
					foreach (var incomingPassage in room.OutgoingPassages)
					{
						if (incomingPassage.EndExit.Room.Id == HIDDEN_ROOM_NUMBER)
						{
							possibleIncomingPassage = incomingPassage;
						}
					}

					continue;
				}

				didSetHiddenRoom = false;
				// It's possible that there are no outgoing exits
				foreach (var passage in room.OutgoingPassages)
				{
					passage.HiddenRoom = GetHiddenRoom();
					didSetHiddenRoom = true;
					break;
				}

				if (didSetHiddenRoom)
				{
					break;
				}
			}

			if (!didSetHiddenRoom && possibleIncomingPassage != null)
			{
				possibleIncomingPassage.HiddenRoom = GetHiddenRoom();
			}
		}

		private Room GetHiddenRoom()
		{
			var roomType = RoomType.Hidden;
			var selectedPreset = _levelData.CurrentLevelType.RoomTypeToPresetsHolder.GetPreset(roomType);
			_roomNumber++;

			int maxX = int.MinValue;
			int maxY = int.MinValue;

			foreach (var room in _idsToPlacedRooms.Values)
			{
				maxX = Mathf.Max(maxX, room.MaxWithFreeSpace.x);
				maxY = Mathf.Max(maxY, room.MaxWithFreeSpace.y);
			}

			var position = new Vector2Int(maxX + _hiddenRoomOffset, maxY + _hiddenRoomOffset);

			var createdRoom = GetNewRoom(selectedPreset, roomType, (uint)_roomNumber, position);
			createdRoom.Initialize(_levelController, _tilemapProvider, _levelData.CurrentLevelSettings.TileProvider);
			return createdRoom;
		}

		private bool CanHaveLoot(Room room)
		{
			return room.RoomType is RoomType.Normal or RoomType.Hub;
		}
	}
}