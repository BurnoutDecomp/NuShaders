-- shadertest.lua
-- Autotest entry for shader analysis
-- TODO: add a teleport phase to jump to specific districts

print("Loading: shadertest.lua")

local Pad      = require "Pad"
local StartGame = require "StartGame"
local Utils    = require "Utils"

function Initialise()
    print("shadertest: getting to car spawn")
    StartGame.Initialise()
end

function Update(StateID)
    local State = Utils.TrimTrailingWhiteSpace(StateID)

    StartGame.Update(State)

    -- once we're ingame, hand the pad back
    if StartGame.GetStatus() == StartGame.E_STARTGAMESTATUS_FINISHED then
        RelinquishPad()
    end
end
