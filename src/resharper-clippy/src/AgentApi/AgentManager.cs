﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using DoubleAgent.Control;
using JetBrains.Application;
using JetBrains.Application.StdApplicationUI;
using JetBrains.Lifetimes;
using JetBrains.Util;
using ActivationContext = CitizenMatt.ReSharper.Plugins.Clippy.AgentApi.SxS.ActivationContext;
using Control = DoubleAgent.Control.Control;

namespace CitizenMatt.ReSharper.Plugins.Clippy.AgentApi
{
    [ShellComponent]
    public class AgentManager
    {
        private readonly Lifetime lifetime;
        private readonly IMainWindow mainWindow;
        private readonly ClippySettingsStore settingsStore;
        private readonly FileSystemPath agentLocation;
        private readonly IDictionary<string, ICharacterEvents> events;
        private readonly IDictionary<int, ICharacterEvents> requests;
        private Control agentControl;

        public AgentManager(Lifetime lifetime, IMainWindow mainWindow, ClippySettingsStore settingsStore)
        {
            this.lifetime = lifetime;
            this.mainWindow = mainWindow;
            this.settingsStore = settingsStore;
            agentLocation = FileSystemPath.Parse(GetType().Assembly.Location).Directory;
            events = new Dictionary<string, ICharacterEvents>();
            requests = new Dictionary<int, ICharacterEvents>();
        }

        public void Initialise()
        {
            if (agentControl != null)
                return;

            var manifestLocation = agentLocation
                .Combine("DoubleAgent." + (IntPtr.Size == 8 ? "x64" : "x86"))
                .Combine("DoubleAgent.sxs.manifest");
            agentControl = ActivationContext.Using(manifestLocation.FullPath, () => new Control());

            agentControl.AutoConnect = 0;
            agentControl.CharacterStyle = (int) (CharacterStyleFlags.IdleEnabled
                | CharacterStyleFlags.Smoothed
                | CharacterStyleFlags.SoundEffects);

            agentControl.CharacterFiles.SearchPath = agentLocation.FullPath;

            agentControl.Click += agentControl_Click;
            agentControl.Command += agentControl_Command;
            agentControl.DblClick += agentControl_DblClick;
            agentControl.DragStart += agentControl_DragStart;
            agentControl.DragComplete += agentControl_DragComplete;
            agentControl.Hide += agentControl_Hide;
            agentControl.Move += agentControl_Move;
            agentControl.RequestStart += agentControl_RequestStart;
            agentControl.RequestComplete += agentControl_RequestComplete;
            agentControl.Show += agentControl_Show;
        }

        private void WithCharacter(string characterId, Action<ICharacterEvents> action)
        {
            ICharacterEvents characterEvents;
            if (events.TryGetValue(characterId, out characterEvents))
                action(characterEvents);
        }

        void agentControl_Click(string characterId, short button, short shift, short x, short y)
        {
            WithCharacter(characterId, c => c.OnClick(button, shift != 0, x, y));
        }

        void agentControl_Command(UserInput userInput)
        {
            WithCharacter(userInput.CharacterID, c => c.OnCommand(userInput));
        }

        void agentControl_DblClick(string characterId, short button, short shift, short x, short y)
        {
            WithCharacter(characterId, c => c.OnDoubleClick(button, shift != 0, x, y));
        }

        void agentControl_DragStart(string characterId, short button, short shift, short x, short y)
        {
            WithCharacter(characterId, c => c.OnDragStart(button, shift != 0, x, y));
        }

        void agentControl_DragComplete(string characterId, short button, short shift, short x, short y)
        {
            WithCharacter(characterId, c => c.OnDragComplete(button, shift != 0, x, y));
        }

        void agentControl_Hide(string characterId, VisibilityCauseType cause)
        {
            WithCharacter(characterId, c => c.OnHide(cause));
        }

        void agentControl_Move(string characterId, short x, short y, MoveCauseType cause)
        {
            WithCharacter(characterId, c => c.OnMove(x, y, cause));
        }

        void agentControl_RequestStart(Request request)
        {
            Debug.WriteLine("Request start: {0} {1}", request.ID, (RequestStatus)request.Status);

            ICharacterEvents agent;
            if(requests.TryGetValue(request.ID, out agent))
                agent.OnRequestStart(request);
        }

        void agentControl_RequestComplete(Request request)
        {
            Debug.WriteLine("Request complete: {0} {1}", request.ID, (RequestStatus)request.Status);

            ICharacterEvents agent;
            if (requests.TryGetValue(request.ID, out agent))
            {
                agent.OnRequestComplete(request);
                requests.Remove(request.ID);
            }
        }

        void agentControl_Show(string characterId, VisibilityCauseType cause)
        {
            WithCharacter(characterId, c => c.OnShow(cause));
        }

        public AgentCharacter GetAgent(string characterName)
        {
            if (agentControl == null)
                Initialise();
            if (agentControl == null)
                return null;

            ICharacterEvents characterEvents;
            if (events.TryGetValue(characterName, out characterEvents) && characterEvents is AgentCharacter)
                return characterEvents as AgentCharacter;

            agentControl.Characters.Load(characterName, characterName + ".acs");
            var character = agentControl.Characters.Character(characterName);

            // Will only be null with TestHarness
            if (settingsStore != null)
            {
                var settings = settingsStore.GetSettings();
                character.SoundEffectsEnabled = settings.SoundEffects;
            }

            var agent = new AgentCharacter(lifetime, character, this, mainWindow.PrimaryWindow.Value, settingsStore);
            events.Add(characterName, agent);
            return agent;
        }

        public void UnloadAgent(AgentCharacter agent)
        {
            var characterId = agent.Character.CharacterID;
            events.Remove(characterId);
            agentControl.Characters.Unload(characterId);
        }

        public void RegisterRequest(Request request, AgentCharacter agentCharacter)
        {
            Debug.WriteLine("Register request: {0} {1}", request.ID, (RequestStatus)request.Status);
            requests.Add(request.ID, agentCharacter);
        }
    }
}