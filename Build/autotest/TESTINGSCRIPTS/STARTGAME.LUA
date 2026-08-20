-- Automated test script module
-- Uses passed in state and returns controller buttons to get user to start of game (i.e. outside junkyard)

print("Loading module: STARTGAME.LUA")

-- Controller constants
local Pad = require "Pad"
local Utils = require "Utils"

-- Predeclaration of upvalues for global functions
local print = print
local PressButton = PressButton

-- Setup module name
module "StartGame"

-- Menu constants
E_STARTGAMESTATUS_NOTSTARTED=0
E_STARTGAMESTATUS_RUNNING=1
E_STARTGAMESTATUS_FINISHED=2

-- Status global
SG_Status = E_STARTGAMESTATUS_NOTSTARTED

-- Global variables
SG_GlobalUpdates = 0
SG_NumMenuUpdates = 0
SG_GotToCarSelect = 0

-- Initialise function
function SG_Initialise ()
	SG_GlobalUpdates = 0							-- Number of global updates we've received
	SG_NumMenuUpdates = 0							-- Number of updates the menus have had
	SG_Status = E_STARTGAMESTATUS_RUNNING
	SG_GotToCarSelect = 0
end

-- Update No Screen
function SG_UpdateNoScreen ()
	if SG_NumMenuUpdates % 2 == 0 then
		return Pad.E_GAMEINPUTACTIONS_GUI_SELECT
	else
		return Pad.E_GAMEINPUTACTIONS_START
	end	
end

-- Update NULL Screen
function SG_UpdateNullScreen ()
	if SG_NumMenuUpdates % 3 == 0 then
		return Pad.E_GAMEINPUTACTIONS_GUI_START
	elseif SG_NumMenuUpdates % 3 == 1 then
		return Pad.E_GAMEINPUTACTIONS_GUI_SELECT
	else
		return Pad.E_GAMEINPUTACTIONS_START
	end
end

-- Update Intro Screen
function SG_UpdateIntroScreen ()
	return Pad.E_GAMEINPUTACTIONS_GUI_SELECT
end

-- Update Chose Vehicle Screen
function SG_UpdateCSVehicleScreen ()
	SG_GotToCarSelect = 1
	return Pad.E_GAMEINPUTACTIONS_GUI_SELECT
end

-- Update Chose Livery Screen
function SG_UpdateCSLiveryScreen ()
	SG_GotToCarSelect = 1
	return Pad.E_GAMEINPUTACTIONS_GUI_SELECT
end

-- Update pad inputs
function SG_UpdateMenuPadInputs (StateID)
	-- Increment function updates
	SG_NumMenuUpdates = SG_NumMenuUpdates + 1
	
	if (StateID==nil) or (StateID=="NULL") then
		return SG_UpdateNullScreen()
	end
	
	-- Update that state
	if StateID=="NOSCREEN" then
		return SG_UpdateNoScreen()
	end
	
	if StateID=="INTRO" then
		return SG_UpdateIntroScreen()
	end
	
	if StateID=="CS_VEHICLE" then
		return SG_UpdateCSVehicleScreen()
	end
	
	if StateID=="CS_LIVERY" then
		return SG_UpdateCSLiveryScreen()
	end
	
	if (SG_GotToCarSelect==0) then
		return Pad.E_GAMEINPUTACTIONS_INVALID
	end
	
	SG_Status = E_STARTGAMESTATUS_FINISHED
	return Pad.E_GAMEINPUTACTIONS_INVALID
end

-- Function to initialize
function Initialise()
	SG_Initialise()
end

-- Function to get status
function GetStatus()
	return SG_Status
end

-- Update testing
function Update (StateID)
	-- Check status - if not running, bail
	if SG_Status ~= E_STARTGAMESTATUS_RUNNING then
		return
	end
	
	-- Increment number of updates
	SG_GlobalUpdates = SG_GlobalUpdates+1
	
	-- Has the counter got to the next update ?
	if SG_GlobalUpdates % 60 == 0 then
	
		-- Call menu pad update
		Result = SG_UpdateMenuPadInputs(Utils.TrimTrailingWhiteSpace(StateID))
		
		-- If user pressed a button, pass it through 
		if Result ~= Pad.E_GAMEINPUTACTIONS_INVALID then
			PressButton(Result)
		end	
	end
	
end
