using Accord.Statistics.Running;
using Class;

namespace AILogic
{
    internal class KalmanPrediction
    {
        public struct Detection
        {
            public int X;
            public int Y;
            public DateTime Timestamp;
        }

        private readonly KalmanFilter2D kalmanFilter = new KalmanFilter2D();
        private DateTime lastFilterUpdateTime = DateTime.UtcNow;

        public void UpdateKalmanFilter(Detection detection)
        {
            kalmanFilter.Push(detection.X, detection.Y);
            lastFilterUpdateTime = DateTime.UtcNow;
        }

        public Detection GetKalmanPosition()
        {
            double timeStep = (DateTime.UtcNow - lastFilterUpdateTime).TotalSeconds;

            double predictedX = kalmanFilter.X + kalmanFilter.XAxisVelocity * timeStep;
            double predictedY = kalmanFilter.Y + kalmanFilter.YAxisVelocity * timeStep;

            return new Detection { X = (int)predictedX, Y = (int)predictedY };
        }
    }

    internal class WiseTheFoxPrediction
    {
        public struct WTFDetection
        {
            public int X;
            public int Y;
            public DateTime Timestamp;
        }

        private const double alpha = 0.5; 
        private const double defaultPredictionTime = 0.07; 

        private const double DampingDistanceThreshold = 60;  
        private const double MinDampingFactor = 0.1;           

        private double emaX;
        private double emaY;

        private double velocityX;
        private double velocityY;

        private DateTime lastUpdateTime;
        private bool initialized = false;

        public void UpdateDetection(WTFDetection detection)
        {
            if (!initialized)
            {
                emaX = detection.X;
                emaY = detection.Y;
                velocityX = 0;
                velocityY = 0;
                lastUpdateTime = detection.Timestamp;
                initialized = true;
                return;
            }

            double dt = (detection.Timestamp - lastUpdateTime).TotalSeconds;
            if (dt <= 0.0001) return;

            double previousEmaX = emaX;
            double previousEmaY = emaY;

            emaX = alpha * detection.X + (1 - alpha) * emaX;
            emaY = alpha * detection.Y + (1 - alpha) * emaY;

            velocityX = (emaX - previousEmaX) / dt;
            velocityY = (emaY - previousEmaY) / dt;

            lastUpdateTime = detection.Timestamp;
        }

        public WTFDetection GetEstimatedPosition(double predictionTime = defaultPredictionTime)
        {
            double predictedX = emaX + velocityX * predictionTime;
            double predictedY = emaY + velocityY * predictionTime;

            double distanceX = predictedX - emaX;
            double distanceY = predictedY - emaY;

           
            double dampingFactorX = 1.0 - Math.Min(Math.Abs(distanceX) / DampingDistanceThreshold, 1.0);
            double dampingFactorY = 1.0 - Math.Min(Math.Abs(distanceY) / DampingDistanceThreshold, 1.0);

           
            dampingFactorX = Math.Max(MinDampingFactor, dampingFactorX);
            dampingFactorY = Math.Max(MinDampingFactor, dampingFactorY);

            predictedX = emaX + velocityX * predictionTime * dampingFactorX;
            predictedY = emaY + velocityY * predictionTime * dampingFactorY;

            return new WTFDetection
            {
                X = (int)predictedX,
                Y = (int)predictedY,
                Timestamp = DateTime.UtcNow
            };
        }
    }


    internal class ShalloePredictionV2
    {
        public static List<int> xValues = [];
        public static List<int> yValues = [];

        public static int AmountCount = 2;

        public static void AddValues(int x, int y)
        {
            if (xValues.Count >= AmountCount)
            {
                xValues.RemoveAt(0);
            }
            if (yValues.Count >= AmountCount)
            {
                yValues.RemoveAt(0);
            }
            xValues.Add(x);
            yValues.Add(y);
        }

        public static int GetSPX()
        {
            return (int)(xValues.Average() * AmountCount + WinAPICaller.GetCursorPosition().X);

        }

        public static int GetSPY()
        {
            return (int)(yValues.Average() * AmountCount + WinAPICaller.GetCursorPosition().Y);
        }
    }

    //internal class HoodPredict
    //{
    //    public static List<int> xValues = [];
    //    public static List<int> yValues = [];

    //    public static int AmountCount = 2;

    //    public static int GetHPX(int CurrentX, int PrevX)
    //    {
    //        int CurrentTime = DateTime.Now.Millisecond;
    //        return 1;
    //    }

    //    public static int GetSPY()
    //    {
    //        return (int)(((Queryable.Average(yValues.AsQueryable()) * AmountCount) + WinAPICaller.GetCursorPosition().Y));
    //    }
    //}
}