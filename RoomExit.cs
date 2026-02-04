using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cornel.LevelGenerating
{
	[Serializable]
	public class RoomExit
	{
		public enum Types
		{
			Normal,
			ToClosedRoom,
			ToClosedCycle,
			ToBossRoom,
			ToShopRoom
		}

		[ShowInInspector]
		public RoomPreset.ExitPreset Preset { get; set; } = null;

		[ShowInInspector]
		public List<RoomExit> OutgoingExits { get; } = new();

		[ShowInInspector]
		public List<RoomExit> IncomingExits { get; } = new();

		[ShowInInspector]
		public Room Room { get; private set; } = null;

		[ShowInInspector]
		public Types Type { get; set; } = Types.Normal;
		
		[ShowInInspector]
		private HashSet<Vector3Int> _tiles = null;
		public HashSet<Vector3Int> Tiles
		{
			get
			{
				if (_tiles == null)
				{
					_tiles = new(3)
						{
							(Vector3Int)LeftWorldPosition
						};

					if (Preset.Direction.x == 0)
					{
						_tiles.Add((Vector3Int)LeftWorldPosition + Vector3Int.left);
						_tiles.Add((Vector3Int)LeftWorldPosition + Vector3Int.right);
						_tiles.Add((Vector3Int)RightWorldPosition + Vector3Int.left);
						_tiles.Add((Vector3Int)RightWorldPosition + Vector3Int.right);
					}
					else
					{
						_tiles.Add((Vector3Int)LeftWorldPosition + Vector3Int.up);
						_tiles.Add((Vector3Int)LeftWorldPosition + Vector3Int.down);
						_tiles.Add((Vector3Int)RightWorldPosition + Vector3Int.up);
						_tiles.Add((Vector3Int)RightWorldPosition + Vector3Int.down);
					}
				}

				return _tiles;
			}
		}

		public bool HasConnectedExits => OutgoingExits.Count > 0 || IncomingExits.Count > 0;

		public RoomExit(Room room, RoomPreset.ExitPreset preset)
		{
			Room = room;
			Preset = preset;
		}

		public Vector2Int LeftWorldPosition => Room.Position + Preset.LeftPosition;
		public Vector2Int RightWorldPosition => Room.Position + Preset.RightPosition;
		public Vector2 CenterWorldPosition => Room.Position + (((Vector2)Preset.LeftPosition + (Vector2)Preset.RightPosition) / 2f);
		public Vector2Int MinWorldPosition => Room.Position +
			new Vector2Int(
				Mathf.Min(Preset.LeftPosition.x, Preset.RightPosition.x),
				Mathf.Min(Preset.LeftPosition.y, Preset.RightPosition.y)
				);
	}
}