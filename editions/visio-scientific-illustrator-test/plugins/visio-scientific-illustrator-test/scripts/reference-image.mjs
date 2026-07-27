export class BridgeReferenceImageBackend {
  constructor(getBridgeClient) {
    if (typeof getBridgeClient !== "function") {
      throw new TypeError("getBridgeClient must be a function.");
    }
    this.getBridgeClient = getBridgeClient;
  }

  async inspectImage({ source_path }) {
    const client = await this.getBridgeClient();
    return client.call("inspect_reference_image", { source_path });
  }

  async cropImage({ source_path, bounds, output_path }) {
    const client = await this.getBridgeClient();
    return client.call(
      "crop_reference_image",
      { source_path, bounds, output_path },
      120000,
    );
  }
}
