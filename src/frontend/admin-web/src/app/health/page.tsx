"use client";

import { useConsulServices, useServiceHealth } from "@/hooks/use-health";
import { useState } from "react";

function HealthStatusBadge({ status }: { status: string }) {
  const colorClass =
    status === "passing"
      ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400"
      : status === "warning"
        ? "bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400"
        : "bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400";

  return (
    <span className={`inline-block px-2 py-0.5 rounded text-xs font-medium ${colorClass}`}>
      {status}
    </span>
  );
}

function ServiceHealthDetail({ serviceName }: { serviceName: string }) {
  const { data: healthEntries, isLoading } = useServiceHealth(serviceName);

  if (isLoading) {
    return <div className="text-xs text-gray-400 mt-2">Loading health checks...</div>;
  }

  if (!healthEntries || healthEntries.length === 0) {
    return <div className="text-xs text-gray-400 mt-2">No health data available</div>;
  }

  return (
    <div className="mt-3 space-y-2">
      {healthEntries.map((entry, i) => (
        <div key={i} className="text-xs space-y-1">
          <div className="text-gray-500">
            Node: {entry.Node.Node} ({entry.Node.Address})
          </div>
          <div className="text-gray-500">
            Instance: {entry.Service.Address || entry.Service.ID}:{entry.Service.Port}
          </div>
          {entry.Checks.map((check, j) => (
            <div key={j} className="flex items-center gap-2">
              <HealthStatusBadge status={check.Status} />
              <span className="text-gray-600 dark:text-gray-400">
                {check.Name}
              </span>
            </div>
          ))}
        </div>
      ))}
    </div>
  );
}

export default function HealthPage() {
  const { data: services, isLoading, error } = useConsulServices();
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const serviceList = services
    ? Object.entries(services).filter(([key]) => key !== "consul")
    : [];

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold">Infrastructure Health</h1>
        <p className="text-sm text-gray-500 mt-1">
          Service discovery status from Consul — auto-refreshes every 10s
        </p>
      </div>

      {isLoading && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {[...Array(3)].map((_, i) => (
            <div
              key={i}
              className="h-32 bg-gray-200 dark:bg-gray-800 rounded-lg animate-pulse"
            />
          ))}
        </div>
      )}

      {error && (
        <div className="rounded-lg border border-red-200 bg-red-50 dark:bg-red-900/20 p-4">
          <p className="text-sm text-red-600 dark:text-red-400">
            Failed to connect to Consul. Make sure the infrastructure is running.
          </p>
        </div>
      )}

      {services && serviceList.length === 0 && (
        <div className="text-center py-12 text-gray-500">
          <p className="text-lg">No services registered</p>
          <p className="text-sm mt-1">
            Start your microservices to see them here.
          </p>
        </div>
      )}

      {serviceList.length > 0 && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {serviceList.map(([id, service]) => (
            <div
              key={id}
              className="border rounded-lg p-4 hover:shadow-md transition-shadow cursor-pointer"
              onClick={() =>
                setExpandedId(
                  expandedId === id ? null : id
                )
              }
            >
              <div className="flex items-center justify-between mb-2">
                <h3 className="font-semibold text-sm">{service.Service}</h3>
                <span className="inline-block w-3 h-3 rounded-full bg-green-500" title="Registered" />
              </div>
              <div className="text-xs text-gray-500 space-y-1">
                <div>ID: {service.ID}</div>
                <div>
                  Address: {service.Address || "N/A"}:{service.Port}
                </div>
                {service.Tags.length > 0 && (
                  <div className="flex gap-1 flex-wrap mt-1">
                    {service.Tags.map((tag) => (
                      <span
                        key={tag}
                        className="bg-gray-100 dark:bg-gray-800 px-1.5 py-0.5 rounded text-xs"
                      >
                        {tag}
                      </span>
                    ))}
                  </div>
                )}
              </div>

              {expandedId === id && (
                <ServiceHealthDetail serviceName={service.Service} />
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
