# E2E: Start Unity app, test HTTP endpoint, then stop

To execute this specification, run

    gauge run specs

## Hello endpoint should respond

* Start server process "Build/u2022-simplehttpserver.exe" on port "18080"
* Wait until endpoint "/api/echo/hello" returns 200 within "30" seconds
* GET "/api/echo/hello?times=2" should return "hellohello"
* Stop server process
