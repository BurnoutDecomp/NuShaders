-- Automated test script module
-- Contains some handy functions for all scripts

print("Loading module: UTILS.LUA")

module("Utils")

-- Trim trailing white space from string
function TrimTrailingWhiteSpace(s)
  local n = #s
  while n > 0 and s:find("^%s", n) do n = n - 1 end
  return s:sub(1, n)
end

