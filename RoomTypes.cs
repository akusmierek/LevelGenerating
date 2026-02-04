using System;

namespace Cornel.LevelGenerating
{
	[Flags]
	public enum RoomType
	{
		Entrance				= 1 << 0,
		Normal					= 1 << 1,
		Hub						= 1 << 2,
		Reward					= 1 << 3,
		Boss					= 1 << 4,
		Shop					= 1 << 5,
		Exit					= 1 << 6,
		PreBoss					= 1 << 7,
		Tutorial_PickUp			= 1 << 8,
		Tutorial_FirstCombat	= 1 << 9,
		Tutorial_SecondCombat	= 1 << 10,
		Tutorial_Dash			= 1 << 11,
		UpgradeTable			= 1 << 12,
		HealRoom				= 1 << 13,
		Hidden					= 1 << 14,
	}
}