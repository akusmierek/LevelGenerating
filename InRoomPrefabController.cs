using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

namespace Cornel.LevelGenerating
{
	public class InRoomPrefabController : MonoBehaviour
	{
		[SerializeField]
		private List<GameObject> _gameObjectsInRoomToInitialize = new();

		protected Room _room = null;

		[Button]
		private void GetGameObjectsToInitialize()
		{
			var objectsInRoom = GetComponentsInChildren<IObjectInRoom>();
			_gameObjectsInRoomToInitialize.Clear();
			foreach (var objectInRoom in objectsInRoom)
			{
				_gameObjectsInRoomToInitialize.Add((objectInRoom as MonoBehaviour).gameObject);
			}
		}

		public virtual void Initialize(Room room)
		{
			_room = room;

			// TODO: Handle chance to spawn
			foreach (var gameObjectInRoom in _gameObjectsInRoomToInitialize)
			{
				if (gameObjectInRoom.TryGetComponent<IObjectInRoom>(out var objectInRoom))
				{
					room.AddObjectInRoom(objectInRoom);
				}
			}

			// TODO:
			//room.SetPathfindingBlocker(x, y);
		}
	}
}