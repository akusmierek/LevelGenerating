using System;
using UnityEngine;

namespace Cornel.LevelGenerating
{
	[Serializable]
	public class Cycle
	{
		// TODO: This class may be actually constructed automatically
		[SerializeField]
		private uint[] _consecutiveIds = null;
		public uint[] ConsecutiveIds => _consecutiveIds;

		public bool IsStartFrom(uint id)
		{
			if (_consecutiveIds == null || _consecutiveIds.Length == 0)
			{
				return false;
			}

			if (_consecutiveIds[0] == id)
			{
				return true;
			}

			return false;
		}
	}
}