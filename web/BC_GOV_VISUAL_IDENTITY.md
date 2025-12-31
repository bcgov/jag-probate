# BC Government Visual Identity

This project implements the BC Government Visual Identity guidelines for consistent branding across government applications.

## Overview

The application uses the official BC Government Bootstrap Theme which includes:
- BC Sans typeface
- BC Government color palette
- Official logos and symbols
- Consistent header and footer components

## Resources

- **Visual Identity Guidelines**: https://www2.gov.bc.ca/gov/content/governments/services-for-government/policies-procedures/bc-visual-identity
- **Bootstrap v5 Theme**: https://github.com/bcgov/bootstrap-v5-theme
- **NPM Package**: https://www.npmjs.com/package/@bcgov/bootstrap-v5-theme

## Components

### NavigationTopbar
Located: `src/components/NavigationTopbar.vue`

Features:
- BC Government logo (desktop and mobile versions)
- Application title
- User profile dropdown

### NavigationFooter
Located: `src/components/NavigationFooter.vue`

Features:
- Standard BC Government footer links:
  - Disclaimer
  - Privacy
  - Accessibility
  - Copyright
  - Contact Us

## Styling

### BC Sans Font
The BC Sans typeface is included via the `@bcgov/bootstrap-theme` package and automatically applied to all text.

Font family stack:
```css
font-family: 'BC Sans', 'Noto Sans', Verdana, Arial, sans-serif;
```

### Color Palette

Primary colors from BC Visual Identity:
- **BC Blue**: `#003366` - Primary brand color
- **BC Gold**: `#fcba19` - Accent color
- **BC White**: `#ffffff` - Background color

### Logos

Located in `public/images/`:
- `bcid-logo-rev-en.svg` - Full logo (reversed for dark backgrounds)
- `bcid-symbol-rev.svg` - Symbol only (for mobile)
- Additional formats available for various use cases

## Implementation

### Package Dependencies

```json
{
  "@bcgov/bootstrap-theme": "https://github.com/bcgov/bootstrap-theme/releases/download/v1.1.3/bcgov-bootstrap-theme-1.1.3.tgz",
  "bootstrap": "^4.3.1",
  "bootstrap-vue": "^2.23.1",
  "sass": "^1.83.0"
}
```

### Styles Import

In `src/main.ts`:
```typescript
import './styles/index.scss';
```

The `index.scss` file imports the BC Gov theme:
```scss
$bcgov-font-path: "../../node_modules/@bcgov/bootstrap-theme/dist/fonts/";
@import "~@bcgov/bootstrap-theme/dist/scss/bootstrap-theme";
```

## Usage

### App Structure

```vue
<template>
  <div class="app-outer">
    <NavigationTopbar />
    <main class="app-main container">
      <router-view />
    </main>
    <NavigationFooter />
  </div>
</template>
```

### Layout Classes

- `.app-outer` - Main app wrapper with flexbox layout
- `.app-main` - Main content area with flex-grow
- `.app-header` - Header styling
- Utility classes for margins: `.mrgn-tp-sm`, `.mrgn-bttm-md`, etc.

## Compliance

This implementation follows the BC Government Visual Identity standards:
- Uses official BC Government colors
- Implements BC Sans typeface
- Includes official BC Government logos
- Provides standard government footer links
- Maintains accessibility standards

## Local Development

Install dependencies:
```bash
npm install --legacy-peer-deps
```

Note: `--legacy-peer-deps` is required due to Bootstrap 4 peer dependency.

Run development server:
```bash
npm run dev
```

## Browser Support

The BC Sans font and visual identity are optimized for:
- Chrome (latest)
- Firefox (latest)
- Safari (latest)
- Edge (latest)

## Additional Resources

- BC Government Digital Toolkit: https://digital.gov.bc.ca/
- BC Design System: https://github.com/bcgov/design-system
- Web Style Guide: https://www2.gov.bc.ca/gov/content/governments/services-for-government/policies-procedures/web-content-development-guides/web-style-guide
