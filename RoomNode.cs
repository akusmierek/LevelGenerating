using System;
using UnityEngine;

namespace Cornel.LevelGenerating
{
	[Serializable]
	public class RoomNode
	{
		[SerializeField]
		private uint _id = 0;
		public uint Id => _id;

		[SerializeField]
		private RoomType _roomType = RoomType.Normal;
		public RoomType RoomType => _roomType;

		[SerializeField]
		private uint _nodeIdToConnectTo = 0;
		public uint NodeIdToConnectTo => _nodeIdToConnectTo;

		// TODO: Additional connections?

        public RoomNode(uint id, RoomType roomType)
        {
			_id = id;
			_roomType = roomType;
        }
    }
}