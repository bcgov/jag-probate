# Web Frontend

Vue 3 + TypeScript frontend for the Probate Management System.

## Technology Stack

- **Vue 3** - Progressive JavaScript framework
- **TypeScript** - Type-safe JavaScript
- **Vue Router** - Official router for Vue.js
- **Pinia** - State management
- **Axios** - HTTP client
- **Bootstrap 4** - CSS framework
- **Vite** - Next generation frontend tooling

## Project Structure

```
web/
├── src/
│   ├── assets/          # Static assets (CSS, images)
│   ├── components/      # Reusable Vue components
│   ├── router/          # Vue Router configuration
│   ├── store/           # Pinia stores
│   ├── views/           # Page components
│   ├── App.vue          # Root component
│   └── main.ts          # Application entry point
├── index.html           # HTML template
├── package.json         # Dependencies
├── tsconfig.json        # TypeScript configuration
└── vite.config.js       # Vite configuration
```

## Development

### Local Development (Outside Docker)

```bash
cd web
npm install
npm run dev
```

The application will be available at http://localhost:1339

### Docker Development

From the `docker/` directory:

```bash
./manage build web-dev
./manage debug
```

Access at http://localhost:8080

## Available Scripts

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run serve` - Preview production build
- `npm run lint` - Lint and fix files
- `npm run test` - Run unit tests

## Routes

- `/` - Home page
- `/cases` - List all probate cases
- `/cases/:id` - View case details
- `/about` - About page with system information

## API Integration

The frontend communicates with the backend API through Axios. API calls are proxied through Vite's dev server:

```javascript
// API requests go through proxy
axios.get('/api/cases')  // → http://api:5000/api/cases
```

### Proxy Configuration

See `vite.config.js` for proxy settings:

```javascript
proxy: {
  '^/api': {
    target: 'http://api:5000',
    changeOrigin: true,
  },
}
```

## Components

### Views

- **HomeView.vue** - Landing page with overview
- **CasesView.vue** - List of all cases with table
- **CaseDetailView.vue** - Detailed view of a single case
- **AboutView.vue** - System information and API health

### App.vue

Root component with navigation bar and router view.

## State Management

Uses Pinia for state management. Stores will be added in `src/store/` as needed.

## Styling

- Bootstrap 4 for component styling
- Custom CSS in `src/assets/main.css`
- Scoped styles in individual components

## Environment Variables

Environment variables can be added to `.env` files:

- `.env` - Default environment variables
- `.env.local` - Local overrides (not committed)
- `.env.production` - Production variables

Access via `import.meta.env.VITE_*`

## Building for Production

```bash
npm run build
```

Dist files will be in `dist/` directory.

## Hot Module Replacement

HMR is enabled by default in development mode. Changes to Vue files will update instantly without full page reload.

## TypeScript

The project uses TypeScript for type safety. Type definitions are in:

- `src/**/*.ts` - TypeScript files
- `src/**/*.vue` - Vue SFC with TypeScript in `<script setup lang="ts">`
- `tsconfig.json` - TypeScript configuration

## Browser Support

- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)

## Troubleshooting

### Port 8080 already in use

Change the port in `docker-compose.yaml`:

```yaml
ports:
  - '8081:1339'  # Use 8081 instead
```

### Module not found errors

```bash
cd web
rm -rf node_modules package-lock.json
npm install
```

### HMR not working in Docker

Ensure polling is enabled:

```yaml
environment:
  - CHOKIDAR_USEPOLLING=true
```
