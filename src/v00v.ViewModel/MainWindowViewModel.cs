﻿using System.Windows.Input;
using v00v.ViewModel.Core;

namespace v00v.ViewModel
{
    public class MainWindowViewModel : ViewModelBase
    {
        private int _count;
        private double _position;

        public int Count
        {
            get => _count;
            set => Update(ref _count, value);
        }

        public double Position
        {
            get => _position;
            set => Update(ref _position, value);
        }

        public ICommand MoveLeftCommand { get; set; }

        public ICommand MoveRightCommand { get; set; }

        public ICommand ResetMoveCommand { get; set; }

        public MainWindowViewModel()
        {
            Count = 0;
            Position = 100.0;
            MoveLeftCommand = new Command((param) => Position -= 5.0);
            MoveRightCommand = new Command((param) => Position += 5.0);
            ResetMoveCommand = new Command((param) => Position = 100.0);
        }

        public void IncrementCount() => Count++;

        public void DecrementCount(object sender, object parameter) => Count--;
    }
}
