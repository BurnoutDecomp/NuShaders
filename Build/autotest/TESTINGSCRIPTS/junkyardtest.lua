-- Automated test script
-- Enters the junk yard and 

print("Loading module: JUNKYARDTEST.LUA")

local Pad = require "Pad"
local StartGame = require "StartGame"
local Utils = require "Utils"


-- Initialise function
function Initialise ()
	print("Running junkyard test")
	StartGame.Initialise()
end

-- Update testing
function Update (StateID)
	
	-- Removing trailing \ leading white space for the state
	State = Utils.TrimTrailingWhiteSpace(StateID)

	-- Run the start game code (this'll do nothing if it's finished)
	StartGame.Update(State)

end


