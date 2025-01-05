.PHONY: kind namespaces app scale-up scale-down k6 all

all: kind namespaces app

kind:
	kind delete cluster --name idcc
	kind create cluster --name idcc --config kind.yaml

namespaces:
	kubectl --context kind-idcc create namespace test

app:
	docker build -t idcc:latest -f ./Dockerfile ../
	kind load docker-image idcc:latest --name idcc
	kubectl -n test --context kind-idcc delete -f deployment.yaml || true
	kubectl -n test --context kind-idcc create -f deployment.yaml

define scale_deployment
	kubectl -n test --context kind-idcc \
	  scale deployment idcc-service \
	  --replicas="$$(($$(kubectl -n test get deployment idcc-service -o=jsonpath='{.spec.replicas}') $1))"
	kubectl -n test --context kind-idcc \
    	  get deployment idcc-service -o wide
endef

scale-up:
	$(call scale_deployment,+1)

scale-down:
	$(call scale_deployment,-1)

k6:
	k6 run --out web-dashboard k6.js
