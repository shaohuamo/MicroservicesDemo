import { useQuery } from "@tanstack/react-query";
import {
  getConsulServices,
  getConsulServiceHealth,
} from "@/lib/api/health";

export function useConsulServices() {
  return useQuery({
    queryKey: ["consul-services"],
    queryFn: getConsulServices,
    refetchInterval: 10_000,
  });
}

export function useServiceHealth(serviceName: string | undefined) {
  return useQuery({
    queryKey: ["consul-health", serviceName],
    queryFn: () => getConsulServiceHealth(serviceName!),
    enabled: !!serviceName,
    refetchInterval: 10_000,
  });
}
