/**
 * Marker interface for component marker classes.
 * Components are named, deployable units of functionality that carry no data or
 * logic themselves — they serve as identifiers for adding/removing functionality
 * to entities.
 */
export interface IComponent {
  readonly componentName: string;
}
