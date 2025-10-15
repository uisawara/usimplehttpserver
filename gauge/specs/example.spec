# E2E: Start Unity app, test HTTP endpoint, then stop

To execute this specification, run

    gauge run specs

## Hello endpoint should respond

* Start server "app1" process "Build/u2022-simplehttpserver.exe" on port "18080"
* Start server "app2" process "Build/u2022-simplehttpserver.exe" on port "18081"
* Wait until endpoint "/api/echo/hello" on "app1" returns 200 within "3" seconds
* GET "/api/echo/hello?times=2" on "app1" should return "hellohello"
* Wait until endpoint "/api/echo/hello" on "app2" returns 200 within "3" seconds
* GET "/api/echo/hello?times=2" on "app2" should return "hellohello"
* Stop server "app1" process
* Stop server "app2" process
