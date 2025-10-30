using beatleader_analyzer;
using beatleader_analyzer.BeatmapScanner.Data;
using beatleader_parser;
using Ratings.AccAi;
using Parser.Map;
using Parser.Map.Difficulty.V3.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Ratings.AccAi.InferPublish;
using Object = UnityEngine.Object;

namespace Ratings
{
    [Plugin("Ratings")]
    public class Plugin
    {
        private UI _ui;

        private const int EditorSceneBuildIndex = 3;
        private const int PollIntervalMilliseconds = 1000; // poll interval

        private static readonly Parse Parser = new();
        private static readonly Analyze Analyzer = new();
        private static readonly AccRating AccRating = new();
        private static readonly Curve Curve = new();

        private readonly InferPublish ai = new();

        private BeatSaberSongContainer? _beatSaberSongContainer;
        private NoteGridContainer? _noteGridContainer;
        private AudioTimeSyncController? _audioTimeSyncController;
        public SongTimelineController? _songTimeLineController;
        private MapEditorUI? _mapEditorUI;

        private List<beatleader_analyzer.BeatmapScanner.Data.Ratings> AnalyzerData = new();
        private List<NoteAcc> AccAiData = new();

        public TextMeshProUGUI Label;

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
                _mapEditorUI = null;
                _noteGridContainer = null;
                _beatSaberSongContainer = null;
                _audioTimeSyncController = null;
                _songTimeLineController = null;

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
                    AccAiData = ai.PredictHitsForMapNotes(diff, _beatSaberSongContainer.Info.BeatsPerMinute, _beatSaberSongContainer.MapDifficultyInfo.NoteJumpSpeed, Config.Timescale);
                }
                else
                {
                    Debug.LogError("Ratings require 20 or more notes to analyze the map. Current note count: " + _noteGridContainer.MapObjects.Count);
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
                AnalyzerData = Analyzer.GetRating(diff, characteristic, difficulty, map.Info._beatsPerMinute, Config.Timescale);
                AccAiData = ai.PredictHitsForMapNotes(diff, _beatSaberSongContainer.Info.BeatsPerMinute, _beatSaberSongContainer.MapDifficultyInfo.NoteJumpSpeed, Config.Timescale);
            }
            else
            {
                Debug.LogError("Ratings require 20 or more notes to analyze the map. Current note count: " + _noteGridContainer.MapObjects.Count);
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
                return;
            }

            float time = _audioTimeSyncController.CurrentJsonTime;
            float seconds = _audioTimeSyncController.CurrentSeconds;

            beatleader_analyzer.BeatmapScanner.Data.Ratings data = AnalyzerData.FirstOrDefault();
            List<PerSwing> timeData = data.PerSwing.Where(x => x.Time >= time).Take(Config.NotesCount).ToList();
            List<NoteAcc> accData = AccAiData.Where(x => x.time >= seconds).Take(Config.NotesCount).ToList();
            if (timeData.Count <= 1 || accData.Count <= 1)
            {
                Label.text = "Not enough data available.";
                return;
            }

            float avgPassRating = (float)timeData.Average(x => x.Pass);
            float avgTechRating = (float)timeData.Average(x => x.Tech);
            float avgAcc = (float)accData.Average(x => x.acc);

            float accRating = AccRating.GetRating(avgAcc, avgPassRating, avgTechRating);
            List<Point> pointList = Curve.GetCurve(avgAcc, accRating);
            float star = Curve.ToStars(0.96f, accRating, avgPassRating, avgTechRating, pointList);
            
            Label.text = "Data from next " + timeData.Count.ToString("F2") + " notes ->" +
                " Pass: " + Math.Round(avgPassRating, 3).ToString("F3") +
                " Tech: " + Math.Round(avgTechRating, 3).ToString("F3") +
                " Acc: " + Math.Round(accRating, 3).ToString("F3") + 
                " Star: " + Math.Round(star, 3).ToString("F3");
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
