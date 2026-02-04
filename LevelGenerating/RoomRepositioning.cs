using Cornel.Utils;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

namespace Cornel.LevelGenerating
{
	public class RoomRepositioning
	{
		public Vector2 Center => (Vector2)_room.Position + (Vector2)Size / 2f;
		public Vector2Int Size => _room.Preset.Size;
		public Vector2Int Min => _room.Position;
		public Vector2Int MinWithFreeSpace => Min - Vector2Int.one * Room.FREE_SPACE_SIZE;
		public Vector2Int Max => _room.Position + Size;
		public Vector2Int MaxWithFreeSpace => Max + Vector2Int.one * Room.FREE_SPACE_SIZE;

		private readonly Room _room = null;

        public RoomRepositioning(Room room)
        {
			_room = room;
        }

        public bool OverlapsWith(Room otherRoom)
		{
			if (otherRoom == _room)
			{
				return false;
			}

			return Utils2D.Overlaps(MinWithFreeSpace, MaxWithFreeSpace, otherRoom.MinWithFreeSpace, otherRoom.MaxWithFreeSpace);
		}

		public Vector2Int GetRepositionVector(Vector2Int otherMin, Vector2Int otherMax)
		{
			Utils2D.GetRepositionVectors(MinWithFreeSpace, MaxWithFreeSpace, otherMin, otherMax,
								out var overlapVector, out var moveDirection);

			if (overlapVector == Vector2Int.zero)
			{
				return Vector2Int.zero;
			}

			// Move only in one axis at once
			Vector2Int moveAwayVector = Vector2Int.one;
			if (overlapVector.x < overlapVector.y)
			{
				moveAwayVector.y = 0;
			}
			if (overlapVector.y < overlapVector.x)
			{
				moveAwayVector.x = 0;
			}

			return moveAwayVector * moveDirection;
		}

		public void Reposition(Vector2Int otherRoomMin, Vector2Int otherRoomMax, List<Room> connectedRoomsToReposition)
		{
			var finalVector = GetRepositionVector(otherRoomMin, otherRoomMax);

			if (finalVector == Vector2Int.zero)
			{
				return;
			}

			foreach (var exit in _room.Exits)
			{
				GetConnectedRoomsToReposition(exit, exit.OutgoingExits, finalVector, connectedRoomsToReposition);
				GetConnectedRoomsToReposition(exit, exit.IncomingExits, finalVector, connectedRoomsToReposition);
			}

			_room.Position += finalVector;
		}

		public void RelaxExits(List<Room> connectedRoomsToReposition)
		{
			var beforePosition = _room.Position;
			foreach (var exit in _room.Exits)
			{
				RelaxConnectedExits(exit, exit.IncomingExits, connectedRoomsToReposition);
				RelaxConnectedExits(exit, exit.OutgoingExits, connectedRoomsToReposition);
			}

			if (_room.Position == beforePosition)
			{
				connectedRoomsToReposition.Clear();
			}
		}

		private void GetConnectedRoomsToReposition(RoomExit sourceExit, List<RoomExit> connectedExits, Vector2Int moveVector, List<Room> connectedRoomsToReposition)
		{
			foreach (var connectedExit in connectedExits)
			{
				if (ShouldRepositionConnectedExit(sourceExit, connectedExit, moveVector))
				{
					if (!connectedRoomsToReposition.Contains(connectedExit.Room))
					{
						connectedRoomsToReposition.Add(connectedExit.Room);
					}
				}
			}
		}

		private bool ShouldRepositionConnectedExit(RoomExit sourceExit, RoomExit targetExit, Vector2Int moveVector)
		{
			if ((sourceExit.Preset.Direction.x * moveVector.x > 0f || targetExit.Preset.Direction.x * moveVector.x < 0f) &&
				AreExitsTooClose(sourceExit, targetExit, Axis.X))
			{
				return true;
			}

			if ((sourceExit.Preset.Direction.y * moveVector.y > 0f || targetExit.Preset.Direction.y * moveVector.y < 0f) &&
				AreExitsTooClose(sourceExit, targetExit, Axis.Y))
			{
				return true;
			}

			return false;
		}

		private bool AreExitsTooClose(RoomExit sourceExit, RoomExit targetExit, Axis axis)
		{
			if (axis == Axis.X)
			{
				return AreExitsTooClose(sourceExit.Preset.Direction.x, targetExit.Preset.Direction.x, sourceExit.CenterWorldPosition.x, targetExit.CenterWorldPosition.x);
			}

			if (axis == Axis.Y)
			{
				return AreExitsTooClose(sourceExit.Preset.Direction.y, targetExit.Preset.Direction.y, sourceExit.CenterWorldPosition.y, targetExit.CenterWorldPosition.y);
			}

			return false;
		}

		private bool AreExitsTooClose(float sourceExitDirection, float targetExitDirection, float sourceExitPosition, float targetExitPosition)
		{
			if (sourceExitDirection == 0 && targetExitDirection == 0)
			{
				return false;
			}

			return Mathf.Abs(targetExitPosition - sourceExitPosition) <= Room.FREE_SPACE_SIZE * 2 + 1;
		}

		private void RelaxConnectedExits(RoomExit sourceExit, List<RoomExit> connectedExits, List<Room> connectedRoomsToReposition)
		{
			foreach (var connectedExit in connectedExits)
			{
				Vector2Int moveVector = Vector2Int.zero;
				bool move = false;

				if (AreExitsTooClose(sourceExit, connectedExit, Axis.X))
				{
					moveVector.x = sourceExit.Preset.Direction.x == 0 ? connectedExit.Preset.Direction.x : -sourceExit.Preset.Direction.x;
					if (moveVector.x == 0)
					{
						moveVector.x = (int)Mathf.Sign(sourceExit.CenterWorldPosition.x - connectedExit.CenterWorldPosition.x);
					}
					move = true;
				}

				if (AreExitsTooClose(sourceExit, connectedExit, Axis.Y))
				{
					moveVector.y = sourceExit.Preset.Direction.y == 0 ? connectedExit.Preset.Direction.y : -sourceExit.Preset.Direction.y;
					if (moveVector.y == 0)
					{
						moveVector.y = (int)Mathf.Sign(sourceExit.CenterWorldPosition.y - connectedExit.CenterWorldPosition.y);
					}
					move = true;
				}

				if (move)
				{
					_room.Position += moveVector;
					connectedRoomsToReposition.Add(connectedExit.Room);
				}
			}
		}
	}
}