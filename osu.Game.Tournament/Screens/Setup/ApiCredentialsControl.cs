// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Bindables;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Tournament.Screens.Setup
{
    internal partial class ApiCredentialsControl : ActionableInfo
    {
        public string ClientId => clientId!.Current.Value;
        public string ClientSecret => clientSecret!.Current.Value;
        public event Action? Changed;

        private FormPasswordTextBox clientId = null!;
        private FormPasswordTextBox clientSecret = null!;

        public ApiCredentialsControl(string id, string secret)
        {
            clientId = new FormPasswordTextBox { Caption = "Client ID", Current = new Bindable<string>(id) };
            clientSecret = new FormPasswordTextBox { Caption = "Client Secret", Current = new Bindable<string>(secret) };
            FlowContainer.Direction = FillDirection.Horizontal;
            FlowContainer.Remove(Button, false);
            FlowContainer.Children = new Drawable[]
            {
                new Container { Width = 180, AutoSizeAxes = Axes.Y, Child = clientId },
                new Container { Width = 280, AutoSizeAxes = Axes.Y, Child = clientSecret },
                Button,
            };
            Button.Anchor = Anchor.CentreLeft;
            Button.Origin = Anchor.CentreLeft;
            clientId.Current.BindValueChanged(_ => updateCredentials());
            clientSecret.Current.BindValueChanged(_ => updateCredentials());
            updateCredentials();
        }

        private void updateCredentials()
        {
            Button.Enabled.Value = !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
            Changed?.Invoke();
        }

    }
}
