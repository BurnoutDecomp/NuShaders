-- Automated test script
-- Takes game online and becomes host. Creates a race
-- and then await players

-- Global variables
GlobalUpdates = 0
NumMenuUpdates = 0
FreeBurnCounter = 0
DrivingTimeOut = 5*60
FreeBurnLobbyTimeOut = 30*60
FreeBurnRaceTimeOut = 20*60
IsOnline = false
IsInOnlineMenu = false
IsOnlineRace = false
IsOnlineLobby = false
HasStartedEvent = false

-- Game input actions
E_GAMEINPUTACTIONS_ACCELERATE=0
E_GAMEINPUTACTIONS_BRAKE=1
E_GAMEINPUTACTIONS_HANDBRAKE=2

E_GAMEINPUTACTIONS_BOOST=3
E_GAMEINPUTACTIONS_CRASHBREAKER=4
E_GAMEINPUTACTIONS_CHANGEVIEW=5
E_GAMEINPUTACTIONS_LOOKBACK=6

E_GAMEINPUTACTIONS_RESET=7
E_GAMEINPUTACTIONS_START=8

E_GAMEINPUTACTIONS_POWERSWERVE_L=9
E_GAMEINPUTACTIONS_POWERSWERVE_R=10

E_GAMEINPUTACTIONS_DIRTY_TRICK=11

E_GAMEINPUTACTIONS_SCREENSHOT=12

E_GAMEINPUTACTIONS_HORN=13

-- Debug Bindings
E_GAMEINPUTACTIONS_DEBUG_UP_PAD=14
E_GAMEINPUTACTIONS_DEBUG_DOWN_PAD=15
E_GAMEINPUTACTIONS_DEBUG_LEFT_PAD=16
E_GAMEINPUTACTIONS_DEBUG_RIGHT_PAD=17
E_GAMEINPUTACTIONS_DEBUG_UP_BUTTON=18
E_GAMEINPUTACTIONS_DEBUG_DOWN_BUTTON=19
E_GAMEINPUTACTIONS_DEBUG_LEFT_BUTTON=20
E_GAMEINPUTACTIONS_DEBUG_RIGHT_BUTTON=21
E_GAMEINPUTACTIONS_DEBUG_LOWER_TRIGGER_LEFT=22
E_GAMEINPUTACTIONS_DEBUG_LOWER_TRIGGER_RIGHT=23
E_GAMEINPUTACTIONS_DEBUG_UPPER_TRIGGER_LEFT=24
E_GAMEINPUTACTIONS_DEBUG_UPPER_TRIGGER_RIGHT=25
E_GAMEINPUTACTIONS_DEBUG_LEFT_STICK_UP=26
E_GAMEINPUTACTIONS_DEBUG_LEFT_STICK_DOWN=27
E_GAMEINPUTACTIONS_DEBUG_LEFT_STICK_LEFT=28
E_GAMEINPUTACTIONS_DEBUG_LEFT_STICK_RIGHT=29
E_GAMEINPUTACTIONS_DEBUG_RIGHT_STICK_UP=30
E_GAMEINPUTACTIONS_DEBUG_RIGHT_STICK_DOWN=31
E_GAMEINPUTACTIONS_DEBUG_RIGHT_STICK_LEFT=32
E_GAMEINPUTACTIONS_DEBUG_RIGHT_STICK_RIGHT=33
E_GAMEINPUTACTIONS_DEBUG_LEFT_STICK_BUTTON=34
E_GAMEINPUTACTIONS_DEBUG_RIGHT_STICK_BUTTON=35
E_GAMEINPUTACTIONS_DEBUG_STEP=36
        
-- GUI Bindings
E_GAMEINPUTACTIONS_GUI_DPAD_UP=37
E_GAMEINPUTACTIONS_GUI_DPAD_DOWN=38
E_GAMEINPUTACTIONS_GUI_DPAD_LEFT=39
E_GAMEINPUTACTIONS_GUI_DPAD_RIGHT=40
E_GAMEINPUTACTIONS_GUI_UP=41
E_GAMEINPUTACTIONS_GUI_DOWN=42
E_GAMEINPUTACTIONS_GUI_LEFT=43
E_GAMEINPUTACTIONS_GUI_RIGHT=44
E_GAMEINPUTACTIONS_GUI_START=45
E_GAMEINPUTACTIONS_GUI_BACK=46
E_GAMEINPUTACTIONS_GUI_LTHUMB=47
E_GAMEINPUTACTIONS_GUI_RTHUMB=48
E_GAMEINPUTACTIONS_GUI_SELECT=49
E_GAMEINPUTACTIONS_GUI_CANCEL=50
E_GAMEINPUTACTIONS_GUI_OPTION0=51
E_GAMEINPUTACTIONS_GUI_OPTION1=52
E_GAMEINPUTACTIONS_GUI_OPTION2=53
E_GAMEINPUTACTIONS_GUI_LSHOULDER=54
E_GAMEINPUTACTIONS_GUI_RSHOULDER=55
E_GAMEINPUTACTIONS_GUI_LTRIGGER=56
E_GAMEINPUTACTIONS_GUI_RTRIGGER=57
E_GAMEINPUTACTIONS_GUI_EVENT_DETAILS=58
E_GAMEINPUTACTIONS_GUI_CAR_LOG=59

E_GAMEINPUTACTIONS_SET_PLAYERSTATS_MAX=60

--     E_GAMEINPUTACTIONS_TEST1,
--     E_GAMEINPUTACTIONS_TEST2,
--     E_GAMEINPUTACTIONS_TEST3,
--     E_GAMEINPUTACTIONS_TEST4,

E_GAMEINPUTACTIONS_COUNT=61

E_GAMEINPUTACTIONS_INVALID = -1


-- Trim trailing white space from string
function TrimTrailingWhiteSpace(s)
  local n = #s
  while n > 0 and s:find("^%s", n) do n = n - 1 end
  return s:sub(1, n)
end


-- Initialise function
function Initialise ()
	GlobalUpdates = 0							-- Number of global updates we've received
	NumMenuUpdates = 0							-- Number of updates the menus have had
	FreeBurnCounter = 0							-- Number of updates in free burn
	IsOnline = false							-- We're not online
	IsInOnlineMenu = false						-- We're not in the online menu
	IsOnlineRace = false						-- Are we in an online race
	IsOnlineLobby = false						-- Are we in the online lobby ?
end

-- Update No Screen
function UpdateNoScreen ()
	if NumMenuUpdates % 2 == 0 then
		return E_GAMEINPUTACTIONS_GUI_SELECT
	else
		return E_GAMEINPUTACTIONS_START
	end
end

-- Update NULL Screen
function UpdateNullScreen ()
	if NumMenuUpdates % 3 == 0 then
		return E_GAMEINPUTACTIONS_GUI_START
	elseif NumMenuUpdates % 3 == 1 then
		return E_GAMEINPUTACTIONS_GUI_SELECT
	else
		return E_GAMEINPUTACTIONS_START
	end
end

-- Update Intro Screen
function UpdateIntroScreen ()
	return E_GAMEINPUTACTIONS_GUI_SELECT
end

-- Update Chose Vehicle Screen
function UpdateCSVehicleScreen ()
	return E_GAMEINPUTACTIONS_GUI_SELECT
end

-- Update Chose Livery Screen
function UpdateCSLiveryScreen ()
	return E_GAMEINPUTACTIONS_GUI_SELECT
end

-- Update main map
function UpdateCnMapMainScreen ()
	if NumMenuUpdates==4 then
		-- Switch to online screen
		NumMenuUpdates = 0
		return E_GAMEINPUTACTIONS_GUI_LSHOULDER
	end
end

-- Update Paradise city Online Screen ()
function UpdateParadiseCityOnlineScreen ()
	if NumMenuUpdates==4 then
		-- Go down one to search
		return E_GAMEINPUTACTIONS_GUI_DOWN
	end
	
	if NumMenuUpdates==6 then
		-- Go down to create
		return E_GAMEINPUTACTIONS_GUI_DOWN
	end
	
	if NumMenuUpdates==8 then
		-- Select this
		return E_GAMEINPUTACTIONS_GUI_SELECT
	end
end

-- Create an Event FSM
function CreateEventScreen ()
	if NumMenuUpdates>2 then
		return E_GAMEINPUTACTIONS_GUI_SELECT
	end
end

-- Update online play screen
function UpdateOnlinePlayScreen ()
	if IsOnline == false then
		if NumMenuUpdates>3 then
			-- Switch to free burn online
			IsOnline = true					-- We're online
			IsInOnlineMenu = false			-- We're not in the menu
			NumMenuUpdates = 0
			return E_GAMEINPUTACTIONS_GUI_SELECT
		end
	end
end

-- Update online play screen
function UpdateOnlineGameOptions ()
	if NumMenuUpdates>2 then
		NumMenuUpdates = 0
		return E_GAMEINPUTACTIONS_GUI_SELECT
	end
end

-- Update online play screen
function UpdateOnlineSelectRoute ()
	print("Update online select route")
	
	if NumMenuUpdates>2 then
		print("Pressing select in Update online select route")
		NumMenuUpdates = 0
		return E_GAMEINPUTACTIONS_GUI_SELECT
	end
	

end

-- Update the game summary
function UpdateOnlineGameSummary ()
	if NumMenuUpdates == 2 then
		IsInOnlineMenu = false
		IsOnlineRace = true
		NumMenuUpdates = 0
		return E_GAMEINPUTACTIONS_GUI_SELECT
	end
end

-- Update the game summary
function UpdateOnlineGameRoom ()
	if NumMenuUpdates > 2 then
		NumMenuUpdates = 0
		return E_GAMEINPUTACTIONS_GUI_SELECT
	end
end

-- Update the on pause screen
function UpdateOnPauseScreen ()
	if NumMenuUpdates == 2 then
		return E_GAMEINPUTACTIONS_GUI_DOWN
	end
	
	if NumMenuUpdates == 3 then
		return E_GAMEINPUTACTIONS_GUI_DOWN
	end
	
	if NumMenuUpdates > 4 then
		NumMenuUpdates = 0
		return E_GAMEINPUTACTIONS_GUI_SELECT
	end
end

-- Update the post results screen
function UpdatePostResultsScreen ()
	if NumMenuUpdates > 1 then
		NumMenuUpdates = 0
		IsOnlineRace = false
		IsInOnlineMenu = false
		HasStartedEvent = false
		FreeBurnCounter = 0
		NumMenuUpdates = 0
		return E_GAMEINPUTACTIONS_GUI_SELECT
	end
end

-- Accept terms and conditions when going online
function UpdateEnterOnlineScreen ()
	if NumMenuUpdates > 1 then
		NumMenuUpdates = 0
		return E_GAMEINPUTACTIONS_GUI_SELECT
	end
end

-- Update pad inputs
function UpdateMenuPadInputs ()
	print(State)
	
	-- Increment function updates
	NumMenuUpdates = NumMenuUpdates + 1
	
	-- Update that state
	if State=="NOSCREEN" then
		return UpdateNoScreen()
	end
	
	if State=="NULL" then
		return UpdateNullScreen()
	end
	
	if State=="INTRO" then
		return UpdateIntroScreen()
	end
	
	if State=="CS_VEHICLE" then
		return UpdateCSVehicleScreen()
	end
	
	if State=="CS_LIVERY" then
		return UpdateCSLiveryScreen()
	end
	
	if State=="CN_MAP_MAIN" then
		return UpdateCnMapMainScreen()
	end
	
	if State=="ON_PLAY" then
		return UpdateOnlinePlayScreen()
	end
	
	if State=="ON_GAME_ROOM" then
		return UpdateOnlineGameRoom()
	end
	
	if State=="ON_GAME_OPT" then
		return UpdateOnlineGameOptions()
	end
	
	if State=="ON_SEL_ROUTE" then
		return UpdateOnlineSelectRoute()
	end
	
	if State=="ON_GAME_SUM" then	
		return UpdateOnlineGameSummary()
	end
	
	if State=="ON_FB_QKCTCR" then
		return UpdateParadiseCityOnlineScreen()
	end
	
	if State=="ON_PAUSE" then
		return UpdateOnPauseScreen()
	end
	
	if State=="POST_ON_IR" then
		return UpdatePostResultsScreen()
	end
	
	if State=="CN_ENTER_ON" then
		return UpdateEnterOnlineScreen()
	end

	
end

-- Update In Game state
function UpdateGamePadInputs ()
	
	-- Update the free burn counter
	FreeBurnCounter = FreeBurnCounter+1
	
	-- Are we driving around off line ? Is it time to go online yet ?
	if FreeBurnCounter>DrivingTimeOut and IsOnlineRace==false then
		-- We need to go to the crash nav to go online
		FreeBurnCounter = 0
		NumMenuUpdates = 0
		
		-- If we are online then the menu goes to overlay
		if IsOnline==true then
			IsInOnlineMenu=true
		end
		
		-- Have we gone into the online lobby yet ?
		if IsOnlineLobby==false then
			-- No, flag we are in it, and go to online freeburn
			IsOnlineLobby = true
			return E_GAMEINPUTACTIONS_GUI_BACK
		end
		
		-- Yes we have, go to create event
		return E_GAMEINPUTACTIONS_GUI_START
	end
	
	-- Are we online and in the free burn lobby ? Is it time to start event
	if FreeBurnCounter>FreeBurnLobbyTimeOut and IsOnlineRace==true and HasStartedEvent==false then
		-- We need to go to the crash nav to start the event
		FreeBurnCounter = 0
		NumMenuUpdates = 0
		
		-- If we are online then the menu goes to overlay
		IsInOnlineMenu=true
		HasStartedEvent=true
		
		return E_GAMEINPUTACTIONS_GUI_START
	end
	
	-- Have we been racing online for a while
	if FreeBurnCounter>FreeBurnRaceTimeOut and IsOnlineRace==true and HasStartedEvent==true then
		-- We need to fire the debug page
		FreeBurnCounter = 0
		NumMenuUpdates = 0
		
		return E_GAMEINPUTACTIONS_GUI_START
	end
	
	-- Update screen	
	return E_GAMEINPUTACTIONS_ACCELERATE
end


-- Update testing
function Update (StateID)
	
	-- Removing trailing \ leading white space for the state
	State = TrimTrailingWhiteSpace(StateID)
	
	-- Increment number of updates
	GlobalUpdates = GlobalUpdates+1
	
	if State=="INGAME" or (State=="ON_GAME_ROOM" and IsInOnlineMenu==false) then
		return UpdateGamePadInputs()
	end

	-- Has the counter got to the next update ?
	if GlobalUpdates % 60 == 0 then
		return UpdateMenuPadInputs()
	end
end
