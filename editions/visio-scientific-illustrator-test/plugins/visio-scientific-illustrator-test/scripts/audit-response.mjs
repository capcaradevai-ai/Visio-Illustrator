export function compactAuditResult(result) {
  const { findings = [], ...summary } = result;
  return {
    ...summary,
    hard_findings: findings
      .filter((finding) => finding.severity === "Hard")
      .map((finding) => ({
        id: finding.id,
        category: finding.category,
        object_ids: finding.object_ids,
        required_change: finding.required_change,
      })),
  };
}

export function compactFindingList(result) {
  return {
    page_revision: result.page_revision,
    findings: (result.findings || []).map((finding) => ({
      id: finding.id,
      severity: finding.severity,
      category: finding.category,
      region_id: finding.region_id,
      object_ids: finding.object_ids,
      observed_revision: finding.observed_revision,
      status: finding.status,
    })),
  };
}
