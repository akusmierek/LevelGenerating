using Sirenix.OdinInspector;
using System.Collections.Generic;
using UnityEngine;

namespace Cornel.LevelGenerating
{
	public class Passage : MonoBehaviour
	{
		[SerializeField, Required, ChildGameObjectsOnly]
		private PlayerDetector _firstPlayerDetector = null;

		[SerializeField, Required, ChildGameObjectsOnly]
		private PlayerDetector _secondPlayerDetector = null;

		public HashSet<Vector3Int> GroundTiles { get; private set; } = new();
		public HashSet<Vector3Int> WallTiles { get; private set; } = new();
		public RoomExit StartExit { get; private set; } = null;
		public RoomExit EndExit { get; private set; } = null;

		public Vector3Int Position => new(_minX, _minY);
		public Vector3Int Size => new(_maxX - _minX + 1, _maxY - _minY + 1);
		public bool WasVisited { get; private set; } = false;
		public Room HiddenRoom { get; set; } = null;

		private LevelController _levelController = null;
		private int _minX = int.MaxValue;
		private int _minY = int.MaxValue;
		private int _maxX = int.MinValue;
		private int _maxY = int.MinValue;

		public void Initialize(LevelController levelController, RoomExit startExit, RoomExit endExit, Vector2 startPosition, Vector2 endPosition)
		{
			_levelController = levelController;

			StartExit = startExit;
			EndExit = endExit;

			_firstPlayerDetector.transform.position = startPosition + Vector2.one / 2f;
			_secondPlayerDetector.transform.position = endPosition + Vector2.one / 2f;

			_firstPlayerDetector.PlayerEntered += OnPassageEntered;
			_secondPlayerDetector.PlayerEntered += OnPassageEntered;
		}

		public void AddTile(Vector3Int position, TileTypes tileType)
		{
            if (tileType == TileTypes.Ground)
            {
				GroundTiles.Add(position);
            }
			else if (tileType == TileTypes.Wall)
			{
				WallTiles.Add(position);
			}

			_minX = Mathf.Min(_minX, position.x);
			_minY = Mathf.Min(_minY, position.y);
			_maxX = Mathf.Max(_maxX, position.x);
			_maxY = Mathf.Max(_maxY, position.y);
		}

		private void OnDestroy()
		{
			if (_firstPlayerDetector != null)
			{
				_firstPlayerDetector.PlayerEntered -= OnPassageEntered;
			}

			if (_secondPlayerDetector != null)
			{
				_secondPlayerDetector.PlayerEntered -= OnPassageEntered;
			}
		}

		private void OnPassageEntered(Player.Player player)
		{
			_levelController.EnterPassage(this, player);
			WasVisited = true;
		}
	}
}