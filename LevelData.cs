using Cornel.LevelGenerating;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Cornel.Player
{
	[CreateAssetMenu(fileName = "LevelData", menuName = "Scriptable Objects/Level Data")]
	public class LevelData : ScriptableObject
	{
		[SerializeField, Required, AssetsOnly]
		private ColorHolder _groundBrightColorHolder = null;

		[SerializeField, Required, AssetsOnly]
		private ColorHolder _groundDarkColorHolder = null;

		public LevelType CurrentLevelType { get; set; } = null;

		private LevelSettings _currentLevelSettings = null;
		public LevelSettings CurrentLevelSettings
		{
			get => _currentLevelSettings;
			set
			{
				_currentLevelSettings = value;
				if (_currentLevelSettings.IsFirstLevel)
				{
					LastRoomId = 0;
				}

				// Set colors
				_groundBrightColorHolder.SetColor(_currentLevelSettings.GroundBrightColor);
				_groundDarkColorHolder.SetColor(_currentLevelSettings.GroundDarkColor);
			}
		}

		public int LastRoomId { get; set; } = 0;
	}
}
