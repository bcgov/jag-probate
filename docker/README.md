## Using the Application

- By default, the main developer UI is exposed at: http://localhost:8080/probate/
- The API is available through the frontend proxy at: http://localhost:8080/api/
- Swagger UI is available at: http://localhost:8080/api/swagger
- The API is also exposed directly at: http://localhost:5000/api/

The local Docker stack relies on the web container's Vite proxy for `/api` routing.
For that to work correctly:

- `WEB_BASE_HREF` must be `/probate/`
- `API_URL` must point at the API root service URL, `http://api:5000`, not `http://api:5000/api/`

# Running the Application on Docker

## Management Script

The `manage` script wraps the Docker process in easy to use commands.

To get full usage information on the script, run:

```
./manage -h
```

### Build all containers
```bash
./manage build
```

### Build specific container
```bash
./manage build api
```

### Start services
```bash
./manage start
```

### Start in debug mode (with hot reload)
```bash
./manage debug
```

### Stop services
```bash
./manage stop
```

### Remove containers and volumes
```bash
./manage down
# or
./manage rm
```
