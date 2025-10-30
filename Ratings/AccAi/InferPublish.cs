﻿using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Parser.Map.Difficulty.V3.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Ratings.AccAi
{
    internal class InferPublish
    {
        private const int BatchSize = 4;
        private const int NumThreads = 4;
        private DataProcessing dataProcessing = new DataProcessing();

        private InferenceSession inferenceSessionAcc = new InferenceSession(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "model_sleep_4LSTM_acc.onnx"), new SessionOptions { IntraOpNumThreads = NumThreads, ExecutionMode = ExecutionMode.ORT_SEQUENTIAL });
        private InferenceSession inferenceSessionSpeed = new InferenceSession(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "model_sleep_4LSTM_speed.onnx"), new SessionOptions { IntraOpNumThreads = NumThreads, ExecutionMode = ExecutionMode.ORT_SEQUENTIAL });

        private static object inferenceSessionLock = new();

        public List<float[]> Predict(List<double[]>[] input, bool speed = false)
        {
            float[] flatInput = input.SelectMany(v => v.SelectMany(v => v.Select(v => (float)v))).ToArray();
            var modelInput = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_1", new DenseTensor<float>(flatInput, new int[] { input.Length, 32, 49 })),
            };

            var outputs = new float[input.Length, 8];

            lock (inferenceSessionLock)
            {
                using (var output = (speed ? inferenceSessionSpeed : inferenceSessionAcc).Run(modelInput, new[] { "time_distributed_2" }))
                {
                    var flatOutput = (output.First().Value as IEnumerable<float>).ToArray();
                    System.Buffer.BlockCopy(flatOutput, 0, outputs, 0, outputs.Length * sizeof(float));
                }
            }

            var listOutputs = new List<float[]>();
            for (int i = 0; i < input.Length; ++i)
            {
                var output = new float[8];
                for (int j = 0; j < 8; ++j)
                {
                    output[j] = outputs[i, j];
                }
                listOutputs.Add(output);
            }
            return listOutputs;
        }

        public (List<float>, List<double>, int) PredictHitsForMap(DifficultyV3 mapdata, double bpm, double njs, double timescale = 1)
        {
            var (segments, noteTimes, freePoints) = dataProcessing.PreprocessMap(mapdata, bpm, njs, timescale);
            if (segments.Count == 0)
            {
                return (new List<float>(), new List<double>(), freePoints);
            }

            var predictionsArraysAcc = new List<float[]>();

            for (int i = 0; i < segments.Count; i += BatchSize)
            {
                var batch = segments.GetRange(i, Math.Min(BatchSize, segments.Count - i)).ToArray();
                if (batch.Length == 0)
                {
                    break;
                }
                var accPrediction = Predict(batch);
                predictionsArraysAcc.AddRange(accPrediction);
            }

            var accs = new List<float>();

            for (int i = 0; i < predictionsArraysAcc.Count; i++)
            {
                var batchPred = predictionsArraysAcc[i];
                var batchInp = segments[i];

                for (int j = 0; j < batchPred.Length; j++)
                {
                    var pred = batchPred[j];
                    var inp = batchInp.Skip(DataProcessing.preSegmentSize).Take(batchInp.Count - DataProcessing.preSegmentSize - DataProcessing.preSegmentSize).ToArray()[0];

                    if (inp.Sum() == 0.0)
                    {
                        continue;
                    }

                    accs.Add(Math.Max(0, pred));
                }
            }

            return (accs, noteTimes, freePoints);
        }

        public class NoteAcc
        {
            public float acc { get; set; }
            public double time { get; set; }
        }

        public  List<NoteAcc> PredictHitsForMapNotes(DifficultyV3 mapdata, double bpm, double njs, double timescale = 1)
        {
            var (accs, noteTimes, freePoints) = PredictHitsForMap(mapdata, bpm, njs, timescale);
            for (int i = 0; i < accs.Count; i++)
            {
                accs[i] = (accs[i] * 15 + 100) / 115;
            }

            return accs.Select((acc, index) => new NoteAcc { acc = acc, time = noteTimes[index] }).ToList();
        }
    }
}
