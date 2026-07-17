using System;
using OpenCvSharp;

namespace ZzzOd.GameLogic.HollowZero;

public sealed record HollowEventDetection(string EventName, double Score, DateTimeOffset CaptureTimeUtc, double RunTime, Mat? Screen = null);
