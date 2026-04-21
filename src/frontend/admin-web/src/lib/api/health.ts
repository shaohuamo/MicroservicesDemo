import { api } from "./http-client";

export interface ConsulService {
  ID: string;
  Service: string;
  Tags: string[];
  Address: string;
  Port: number;
  Meta: Record<string, string>;
}

export interface ConsulHealthEntry {
  Node: {
    Node: string;
    Address: string;
  };
  Service: {
    ID: string;
    Service: string;
    Tags: string[];
    Address: string;
    Port: number;
  };
  Checks: {
    CheckID: string;
    Name: string;
    Status: string; // "passing" | "warning" | "critical"
    Output: string;
  }[];
}

export async function getConsulServices(): Promise<
  Record<string, ConsulService>
> {
  const { data } = await api.get<Record<string, ConsulService>>(
    "/consul/agent/services"
  );
  return data;
}

export async function getConsulServiceHealth(
  serviceName: string
): Promise<ConsulHealthEntry[]> {
  const { data } = await api.get<ConsulHealthEntry[]>(
    `/consul/health/service/${serviceName}`
  );
  return data;
}
