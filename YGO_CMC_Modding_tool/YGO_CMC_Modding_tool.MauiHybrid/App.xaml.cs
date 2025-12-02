namespace YGO_CMC_Modding_tool.MauiHybrid
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new MainPage()) { Title = "YGO_CMC_Modding_tool" };
        }
    }
}
