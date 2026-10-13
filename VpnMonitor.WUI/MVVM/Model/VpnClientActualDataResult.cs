using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace VpnMonitor.WUI.MVVM.Model
{
    public class VpnClientActualDataResult : ObservableObject
    {
        public string Client { get; set; }

        public bool IsOnline { get; set; }

        public string LastSend { get; set; }

        public string LasrReceived { get; set; }

        public double ConnectedTime { get; set; }

        public DateTime? LastConnected { get; set; }

        public DateTime? LastSeen { get; set; }

        public VpnClientActualDataResult(string client)
        {
            Client = client;
            IsOnline = false;
            LastSend = "0.0";
            LasrReceived = "0.0";
            ConnectedTime = 0;
        }

        public override string ToString()
        {
            var onlineStr = IsOnline ? "online" : "offline";
            return $"{Client}({onlineStr}) - send: {LastSend} received: {LasrReceived}";
        }

        public string ViewTitleColor { get => IsOnline ? "Green" : "Gray"; }

        public string ViewTitle { get => $"{Client}"; }

        public string ViewConnectedTime { get => $"{ConnectedTime} мин"; }

        public string ViewStatus { get => IsOnline ? ViewConnectedTimeV2 : $"Не в сети с {LastSeen:dd.MM.yyyy HH:mm}"; }

        public string ViewLastConnected { get => $"Последнее подключение: {LastConnected:dd.MM.yyyy HH:mm}"; }

        public string ViewConnectedTimeV2
        {
            get
            {
                var roundedTime = (int)Math.Round(ConnectedTime);

                // Обрабатываем отрицательные значения
                bool isNegative = roundedTime < 0;
                int absMinutes = Math.Abs(roundedTime);

                // Вычисляем компоненты времени
                int days = absMinutes / (24 * 60);          // Полные дни
                int remainingMinutes = absMinutes % (24 * 60);
                int hours = remainingMinutes / 60;             // Часы
                int minutes = remainingMinutes % 60;         // Минуты

                string result;

                // Формируем строку по приоритету: дни → часы → минуты
                if (days > 0)
                {
                    result = $"{days} дн. {hours} ч. {minutes} мин.";
                }
                else if (hours > 0)
                {
                    result = $"{hours} ч. {minutes} мин.";
                }
                else
                {
                    result = $"{minutes} мин.";
                }

                return isNegative ? "-" + result : result;
            }
        }

    }
}
