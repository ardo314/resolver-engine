# Engine

## License

This project is **not open source**.

The source code is publicly visible for evaluation purposes only.
You may not use, run, modify, or distribute this software without
explicit written permission from the author.

See the [LICENSE](LICENSE) file for details.

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
