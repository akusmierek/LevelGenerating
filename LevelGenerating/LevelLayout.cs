using Cornel.Hub;
using Cornel.Player;
using Cornel.Utils;
using Sirenix.OdinInspector;
using System;
using UnityEngine;

namespace Cornel.LevelGenerating
{
	[CreateAssetMenu(fileName = "LevelLayout", menuName = "Scriptable Objects/Level Generation/LevelLayout")]
	public class LevelLayout : ScriptableObject
	{
		[Serializable]
		public class AdditionalRoom
		{
			[Flags]
			public enum Type
			{
				PercentChance = 1,
				GameUpgrade = 2,
			}

			[SerializeField]
			private Type _type = Type.PercentChance;

			[SerializeField, MinValue(0f), MaxValue(100f), Unit(Units.Percent), ShowIf(nameof(ShowChancePercent))]
			private float _chancePercent = 100f;

			[SerializeField, Required, AssetsOnly, ShowIf(nameof(ShowGameUpgrade))]
			private ProgressData _progressData = null;

			[SerializeField, Required, AssetsOnly, ShowIf(nameof(ShowGameUpgrade))]
			private GameUpgradeData _gameUpgrade = null;

			[SerializeField]
			private RoomType _roomType = RoomType.Shop;
			public RoomType RoomType => _roomType;

			[SerializeField]
			private RoomType _possibleConnections = RoomType.Normal | RoomType.Hub | RoomType.Reward | RoomType.Shop;
			public RoomType PossibleConnections => _possibleConnections;

			private bool ShowChancePercent => _type.HasFlag(Type.PercentChance);
			private bool ShowGameUpgrade => _type.HasFlag(Type.GameUpgrade);

			public bool CanPlace()
			{
				bool canPlace = true;

				if (_type.HasFlag(Type.PercentChance))
				{
					canPlace = canPlace && RandomUtils.NextInt(0, 100) <= _chancePercent;
				}

				if (_type.HasFlag(Type.GameUpgrade))
				{
					canPlace = canPlace && _progressData.IsGameUpgradeUnlocked(_gameUpgrade);
				}

				return canPlace;
			}
		}

		[Serializable]
		public class Group
		{
			[SerializeField]
			private bool _isCycle = false;
			public bool IsCycle => _isCycle;

			[SerializeField]
			private bool _isClosed = false;
			public bool IsClosed => _isClosed;

			[SerializeField]
			private RoomNode[] _nodes = null;
			public RoomNode[] Nodes => _nodes;
		}

		[SerializeField]
		private Group[] _groups = null;
		public Group[] Groups => _groups;

		[SerializeField]
		private AdditionalRoom[] _additionalRooms = null;
		public AdditionalRoom[] AdditionalRooms => _additionalRooms;
	}
}