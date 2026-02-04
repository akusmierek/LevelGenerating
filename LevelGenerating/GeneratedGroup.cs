using System.Collections.Generic;
using UnityEngine;

namespace Cornel.LevelGenerating
{
	public class GeneratedGroup
	{
		public List<Room> Rooms { get; set; } = new();
		public LevelLayout.Group LayoutGroup { get; set; } = null;

		public void LockRepositioning()
		{
			foreach (var room in Rooms)
			{
				room.CanReposition = false;
			}
		}

		public void SetVisible(bool isVisible)
		{
			foreach (var room in Rooms)
			{
				room.gameObject.SetActive(isVisible);
			}
		}

		public void Move(Vector2Int offset)
		{
			foreach (var room in Rooms)
			{
				room.Position += offset;
			}
		}

		public RoomExit GetExitAimingOutsideGroup(Room connectedRoom)
		{
			Vector2 centerOfTheGroup = Vector2.zero;
			foreach (var room in Rooms)
			{
				centerOfTheGroup += room.Center;
			}

			centerOfTheGroup /= Rooms.Count;
			var directionOutsideCenter = connectedRoom.Center - centerOfTheGroup;

			return connectedRoom.GetExitToNewRoom(directionOutsideCenter, 0f);
		}

		public bool OverlapsWith(GeneratedGroup otherGroup, out Room overlappingRoom, out Room otherOverlappingRoom)
		{
			overlappingRoom = null;
			otherOverlappingRoom = null;

			foreach (var room in Rooms)
			{
				foreach (var otherRoom in otherGroup.Rooms)
				{
					if (room.OverlapsWith(otherRoom))
					{
						overlappingRoom = room;
						otherOverlappingRoom = otherRoom;

						return true;
					}
				}
			}

			return false;
		}
	}
}