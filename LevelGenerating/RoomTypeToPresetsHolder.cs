using Cornel.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Cornel.LevelGenerating
{
	[CreateAssetMenu(fileName = "RoomTypeToPresetsHolder", menuName = "Scriptable Objects/Level Generation/Room Type To Presets Holder")]
	public class RoomTypeToPresetsHolder : ScriptableObject
	{
		[SerializeField]
		private SerializableDictionary<RoomType, RoomPreset[]> _roomTypeToRoomPresets = null;

		public RoomPreset GetPreset(RoomType roomType)
		{
			var possiblePresets = _roomTypeToRoomPresets[roomType];
			return possiblePresets.ElementAt(RandomUtils.NextInt(0, possiblePresets.Count()));
		}

		public RoomPreset GetUniquePreset(RoomType roomType, HashSet<RoomPreset> usedPresets)
		{
			var possiblePresets = _roomTypeToRoomPresets[roomType].Except(usedPresets);

			if (possiblePresets.Count() <= 0)
			{
				Debug.LogWarning("Couldn't find enough unique room presets. Continuing with non-unique rooms.");
				possiblePresets = _roomTypeToRoomPresets[roomType];
			}

			return possiblePresets.ElementAt(RandomUtils.NextInt(0, possiblePresets.Count()));
		}
	}
}