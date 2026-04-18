# Engine

## License

This project is **not open source**.

The source code is publicly visible for evaluation purposes only.
You may not use, run, modify, or distribute this software without
explicit written permission from the author.

See the [LICENSE](LICENSE) file for details.

## Running Locally

Start each service in a separate terminal inside the devcontainer:

```bash
npm run build                              # compile TypeScript (or npm run watch)
nats-server -c nats.conf                   # start NATS
node engine/backend/dist/index.js          # start backend
node workers/in-memory/dist/index.js       # start in-memory worker host
cd engine/editor && npm run dev -- --host  # start Vite dev server
```

The editor is available at <http://localhost:5173>.
NATS monitoring is available at <http://localhost:8222>.

## Building & Pushing Images

Images are built with [Skaffold](https://skaffold.dev). The configuration
lives in `skaffold.yaml` and produces three images:

| Image                                      | Dockerfile                    |
| ------------------------------------------ | ----------------------------- |
| `ghcr.io/ardo314/component-engine-backend` | `engine/backend/Dockerfile`   |
| `ghcr.io/ardo314/component-engine-editor`  | `engine/editor/Dockerfile`    |
| `ghcr.io/ardo314/component-engine-nova`    | `deployments/nova/Dockerfile` |

### Prerequisites

- Docker logged in to `ghcr.io` (`docker login ghcr.io`)
- [Skaffold](https://skaffold.dev/docs/install/) installed

### Build and push all images

```bash
skaffold build
```

The nova image automatically receives the full backend and editor image
references (including tags) as build args, so the deployment always
points to the exact images built in the same run.
