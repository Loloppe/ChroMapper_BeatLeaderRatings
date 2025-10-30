using beatleader_analyzer;
using beatleader_analyzer.BeatmapScanner.Data;
using beatleader_parser;
using BeatLeaderRatings.AccAi;
using Parser.Map;
using Parser.Map.Difficulty.V3.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BeatLeaderRatings
{
    [Plugin("BeatLeaderRatings")]
    public class Plugin
    {
        private UI _ui;

        private const int EditorSceneBuildIndex = 3;
        private const int PollIntervalMilliseconds = 1000; // poll interval

        public static readonly Parse Parser = new();
        public static readonly Analyze Analyzer = new();

        private readonly InferPublish ai = new();

        private BeatSaberSongContainer? _beatSaberSongContainer;
        private NoteGridContainer? _noteGridContainer;
        private AudioTimeSyncController? _audioTimeSyncController;
        private SongTimelineController? _songTimeLineController;
        private MapEditorUI? _mapEditorUI;

        private List<Ratings> AnalyzerData = new();
        private Dictionary<string, object> AccAiData = new();

        private TextMeshProUGUI Label;

        [Init]
        private void Init()
        {
            SceneManager.sceneLoaded += SceneLoaded;
            _ui = new UI(this);
        }

        private async void SceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.buildIndex == EditorSceneBuildIndex)
            {
                _noteGridContainer = null;
                _beatSaberSongContainer = null;
                _audioTimeSyncController = null;
                _songTimeLineController = null;
                _mapEditorUI = null;

                await FindObject();

                BeatmapV3 map = Parser.TryLoadPath(_beatSaberSongContainer.Info.Directory);
                string characteristic = _beatSaberSongContainer.MapDifficultyInfo.Characteristic;
                string difficulty = _beatSaberSongContainer.MapDifficultyInfo.Difficulty;
                DifficultyV3 diff = map.Difficulties.FirstOrDefault(x => x.Difficulty == difficulty && x.Characteristic == characteristic).Data;

                _Difficultybeatmaps difficultyBeatmap = map.Info._difficultyBeatmapSets.FirstOrDefault(x => x._beatmapCharacteristicName == characteristic)._difficultyBeatmaps.FirstOrDefault(x => x._difficulty == difficulty);
                int diffCount = map.Info._difficultyBeatmapSets.FirstOrDefault(x => x._beatmapCharacteristicName == characteristic)._difficultyBeatmaps.Count();
                
                if (_noteGridContainer.MapObjects.Count >= 20)
                {
                    AnalyzerData = Analyzer.GetRating(diff, characteristic, difficulty, map.Info._beatsPerMinute, Config.Timescale);

                    AccAiData = ai.PredictHitsForMapNotes(diff, _beatSaberSongContainer.Info.BeatsPerMinute, Config.Timescale);
                }
                else
                {
                    Debug.LogError("BeatLeaderRatings require 20 or more notes to analyze the map. Current note count: " + _noteGridContainer.MapObjects.Count);
                }

                _audioTimeSyncController.TimeChanged += OnTimeChanged;

                if (Label == null)
                {
                    ApplyUI();
                }

                _ui.AddMenu(_mapEditorUI);
            }
        }

        public void Reload()
        {
            BeatmapV3 map = Parser.TryLoadPath(_beatSaberSongContainer.Info.Directory);
            string characteristic = _beatSaberSongContainer.MapDifficultyInfo.Characteristic;
            string difficulty = _beatSaberSongContainer.MapDifficultyInfo.Difficulty;
            DifficultyV3 diff = map.Difficulties.FirstOrDefault(x => x.Difficulty == difficulty && x.Characteristic == characteristic).Data;

            _Difficultybeatmaps difficultyBeatmap = map.Info._difficultyBeatmapSets.FirstOrDefault(x => x._beatmapCharacteristicName == characteristic)._difficultyBeatmaps.FirstOrDefault(x => x._difficulty == difficulty);
            int diffCount = map.Info._difficultyBeatmapSets.FirstOrDefault(x => x._beatmapCharacteristicName == characteristic)._difficultyBeatmaps.Count();

            if (_noteGridContainer.MapObjects.Count >= 20)
            {
                AnalyzerData = Analyzer.GetRating(diff, characteristic, difficulty, map.Info._beatsPerMinute);
            }
            else
            {
                Debug.LogError("BeatLeaderRatings require 20 or more notes to analyze the map. Current note count: " + _noteGridContainer.MapObjects.Count);
            }
        }

        private async Task FindObject()
        {
            while (_noteGridContainer == null || _beatSaberSongContainer == null || _audioTimeSyncController == null || _songTimeLineController == null || _mapEditorUI == null)
            {
                await Task.Delay(PollIntervalMilliseconds);
                _noteGridContainer = _noteGridContainer ?? Object.FindObjectOfType<NoteGridContainer>();
                _beatSaberSongContainer = _beatSaberSongContainer ?? Object.FindObjectOfType<BeatSaberSongContainer>();
                _audioTimeSyncController = _audioTimeSyncController ?? Object.FindObjectOfType<AudioTimeSyncController>();
                _songTimeLineController = _songTimeLineController ?? Object.FindObjectOfType<SongTimelineController>();
                _mapEditorUI = _mapEditorUI ?? Object.FindObjectOfType<MapEditorUI>();
            }
        }

        private void OnTimeChanged()
        {
            if (!Config.Enabled)
            {
                Label.text = "";
                return;
            }

            var notes = _noteGridContainer.MapObjects.OrderBy(o => o.JsonTime).ToList();
            var time = _audioTimeSyncController.CurrentJsonTime;

            var data = AnalyzerData.FirstOrDefault();
            var timeData = data.PerSwing.Where(x => x.Time >= time).Take(Config.NotesCount).ToList();
            if (timeData.Count <= 1)
            {
                Label.text = "Not enough data available.";
                return;
            }

            float avgPassRating = (float)timeData.Average(x => x.Pass);
            float avgTechRating = (float)timeData.Average(x => x.Tech);

            
            AccRating ar = new();
            float accRating = ar.GetRating(predictedAcc, avgPassRating, avgTechRating);
            Curve curve = new();
            var pointList = curve.GetCurve(predictedAcc, accRating);
            var star = curve.ToStars(0.96f, accRating, avgPassRating, avgTechRating, pointList);
            
            Label.text = "Data from next " + timeData.Count + " notes ->" +
                " Pass: " + Math.Round(avgPassRating, 3).ToString() +
                " Tech: " + Math.Round(avgTechRating, 3).ToString() +
                " Acc: " + Math.Round(accRating, 3).ToString() + 
                " Star: " + Math.Round(star, 3).ToString();
        }

        private void ApplyUI()
        {
            TextMeshProUGUI songTimeText = _songTimeLineController.transform.Find("Song Time").GetComponent<TextMeshProUGUI>();

            Label = Object.Instantiate(songTimeText, songTimeText.transform.parent);
            Label.rectTransform.localPosition = new Vector2(-210f, 36f);
            Label.alignment = TextAlignmentOptions.BottomLeft;
            Label.fontSize = 17;
            Label.text = "";
        }
    }
}
