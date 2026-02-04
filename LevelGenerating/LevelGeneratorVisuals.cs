using DG.Tweening;
using Sirenix.OdinInspector;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Cornel.LevelGenerating
{
	public class LevelGeneratorVisuals : MonoBehaviour
	{
		[SerializeField]
		private LevelGenerator _levelGenerator = null;

		[SerializeField]
		private Canvas _canvas = null;

		[SerializeField]
		private Image _black = null;

		[SerializeField, MinValue(0f), Unit(Units.Second)]
		private float _timeToFadeOut = 1f;

		[SerializeField]
		private LocalizedString _generatingLevelString = null;

		[SerializeField]
		private TextMeshProUGUI _text = null;

		[SerializeField, MinValue(0f), Unit(Units.Second)]
		private float _textUpdatePeriod = 1f;

		private float _nextTextUpdateTime = 0;
		private int _numberOfDots = 1;

		private void Start()
		{
			if (_levelGenerator.IsGenerating)
			{
				_canvas.gameObject.SetActive(true);

				LevelGenerator.LevelGenerated += OnLevelGenerated;
			}
		}

		private void OnDestroy()
		{
			LevelGenerator.LevelGenerated -= OnLevelGenerated;
		}

		private void Update()
		{
			if (_levelGenerator.IsGenerating && _nextTextUpdateTime <= Time.time)
			{
				var text = _generatingLevelString.GetLocalizedString();
				for ( int i = 0; i < _numberOfDots; i++)
				{
					text += ".";
				}

				_text.text = text;

				_numberOfDots++;
				if (_numberOfDots > 3)
				{
					_numberOfDots = 1;
				}

				_nextTextUpdateTime = Time.time + _textUpdatePeriod;
			}
		}

		private void OnLevelGenerated()
		{
			_text.enabled = false;

			_black.DOFade(0f, _timeToFadeOut).OnComplete(DisableCanvas);
		}

		private void DisableCanvas()
		{
			_canvas.gameObject.SetActive(false);
		}
	}
}