#!/usr/bin/env python3
"""Concurrent App Service page or telemetry-ingestion load check."""
import argparse, concurrent.futures, csv, json, random, statistics, time, urllib.error, urllib.request, uuid

def one(url, timeout, mode, api_key, index):
    start=time.perf_counter()
    try:
        headers={"User-Agent":"AgriStream-LoadTest/1.0"}
        data=None
        if mode=="telemetry":
            headers.update({"Content-Type":"application/json","X-AgriStream-Key":api_key})
            data=json.dumps({"farmId":"loadtest-farm","deviceId":f"loadtest-{index%50:03d}","soilMoisture":round(random.uniform(10,80),1),"temperatureC":round(random.uniform(12,36),1),"humidityPercent":round(random.uniform(20,90),1)}).encode()
        request=urllib.request.Request(url,data=data,headers=headers,method="POST" if data else "GET")
        with urllib.request.urlopen(request,timeout=timeout) as response:
            response.read(4096)
            return response.status,(time.perf_counter()-start)*1000
    except urllib.error.HTTPError as e:
        return e.code,(time.perf_counter()-start)*1000
    except Exception:
        return 0,(time.perf_counter()-start)*1000

def percentile(values,p):
    values=sorted(values)
    return values[min(len(values)-1,int((len(values)-1)*p))]

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("base_url",help="Deployed site URL, e.g. https://your-app.azurewebsites.net")
    parser.add_argument("--requests",type=int,default=300)
    parser.add_argument("--concurrency",type=int,default=25)
    parser.add_argument("--timeout",type=int,default=30)
    parser.add_argument("--mode",choices=("page","telemetry"),default="page")
    parser.add_argument("--api-key",default="",help="Required for telemetry mode; matches Azure:SensorApiKey")
    parser.add_argument("--csv",default="load-results.csv")
    args=parser.parse_args()
    if args.requests<1 or args.concurrency<1: parser.error("requests and concurrency must be positive")
    if args.mode=="telemetry" and not args.api_key: parser.error("--api-key is required with --mode telemetry")
    url=args.base_url.rstrip("/")+("/Account/Login" if args.mode=="page" else "/api/sensors/telemetry")
    start=time.perf_counter()
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.concurrency) as pool:
        results=list(pool.map(lambda i:one(url,args.timeout,args.mode,args.api_key,i),range(args.requests)))
    elapsed=time.perf_counter()-start
    latencies=[ms for _,ms in results]
    success=sum(200<=status<400 for status,_ in results)
    with open(args.csv,"w",newline="",encoding="utf-8") as f:
        writer=csv.writer(f);writer.writerow(["status_code","latency_ms"]);writer.writerows(results)
    print(f"Mode: {args.mode}; target: {url}\nRequests: {args.requests}; concurrency: {args.concurrency}; elapsed: {elapsed:.2f}s")
    print(f"Successful (2xx/3xx): {success}; errors: {args.requests-success}; throughput: {args.requests/elapsed:.2f} req/s")
    print(f"Latency ms: mean {statistics.mean(latencies):.1f}; p50 {percentile(latencies,.50):.1f}; p95 {percentile(latencies,.95):.1f}; p99 {percentile(latencies,.99):.1f}; max {max(latencies):.1f}")
    print(f"Per-request results: {args.csv}")

if __name__=="__main__": main()
