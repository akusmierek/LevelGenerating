using Cornel.LevelGenerating;
using FMODUnity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Cornel.Player
{
	[CreateAssetMenu(fileName = "LevelSettings", menuName = "Scriptable Objects/Level Settings")]
	public class LevelSettings : ScriptableObject
	{
		[SerializeField]
		private bool _isFirstLevel = false;
		public bool IsFirstLevel => _isFirstLevel;

		[SerializeField, ShowIf(nameof(_isFirstLevel))]
		private LevelType[] _possibleFirstLevelTypes = null;
		public LevelType[] PossibleFirstLevelTypes => _possibleFirstLevelTypes;

		[SerializeField]
		private LevelType[] _possibleNextLevelTypes = null;
		public LevelType[] PossibleNextLevelTypes => _possibleNextLevelTypes;

		[SerializeField, MinValue(1)]
		private int _nextLevelCardsCount = 3;
		public int NextLevelCardsCount => _nextLevelCardsCount;

		[SerializeField]
		private string _nextSceneName = string.Empty;
		public string NextSceneName => _nextSceneName;

		[SerializeField, Required, AssetsOnly]
		private TileProvider _tileProvider = null;
		public TileProvider TileProvider => _tileProvider;

		[SerializeField]
		private float _enemiesHealthMultiplier = 1f;
		public float EnemiesHealthMultiplier => _enemiesHealthMultiplier;

		[Title("Audio")]
		[SerializeField]
		private EventReference _music;
		public EventReference Music => _music;

		[SerializeField]
		private EventReference _ambient;
		public EventReference Ambient => _ambient;

		[Title("Colors")]
		[SerializeField, Required, AssetsOnly]
		private ColorHolder _groundBrightColor = null;
		public ColorHolder GroundBrightColor => _groundBrightColor;

		[SerializeField, Required, AssetsOnly]
		private ColorHolder _groundDarkColor = null;
		public ColorHolder GroundDarkColor => _groundDarkColor;
	}
}
