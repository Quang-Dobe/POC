import openai
from azure.identity import ClientSecretCredential
from azure.ai.projects import AIProjectClient
from .config import Config

# Tool schema sent to the model on every request.
QUERY_DAB_TOOL_DEF = {
    "type": "function",
    "function": {
        "name": "query_dab",
        "description": (
            "Query read-only NYC taxi data from Data API Builder. "
            "Entities: Trip, Date, Geography, Weather."
        ),
        "parameters": {
            "type": "object",
            "properties": {
                "entity": {
                    "type": "string",
                    "enum": ["Trip", "Date", "Geography", "Weather"],
                },
                "filter": {
                    "type": "string",
                    "description": "OData $filter expression, e.g. DateID eq 20130101",
                },
                "select": {
                    "type": "string",
                    "description": "Comma-separated field names, e.g. DateID,TotalAmount",
                },
                "orderby": {
                    "type": "string",
                    "description": "Sort expression, e.g. TotalAmount desc",
                },
                "first": {
                    "type": "integer",
                    "default": 20,
                    "description": "Max rows. Capped at 50 server-side.",
                },
            },
            "required": ["entity"],
        },
    },
}

AGENT_INSTRUCTIONS = """You answer questions about NYC taxi data using the query_dab tool.

Entity schemas (only these fields exist — never invent others):
- Trip: DateID, MedallionID, HackneyLicenseID, PickupTimeID, DropoffTimeID, PickupGeographyID, DropoffGeographyID, PickupLatitude, PickupLongitude, PickupLatLong, DropoffLatitude, DropoffLongitude, DropoffLatLong, PassengerCount, TripDurationSeconds, TripDistanceMiles, PaymentType, FareAmount, SurchargeAmount, TaxAmount, TipAmount, TollsAmount, TotalAmount
- Date: DateID, Date, DateBKey, DayOfMonth, DayName, DayOfWeek, Month, MonthName, Quarter, Year, IsHolidayUSA, IsWeekday, HolidayUSA
- Geography: GeographyID, ZipCodeBKey, County, City, State, Country, ZipCode
- Weather: DateID, GeographyID, PrecipitationInches, AvgTemperatureFahrenheit

Rules:
- first must be >= 1. Default 20, max 50. No total count endpoint exists — if asked for total count, say results are paginated and show a sample.
- Use $filter for WHERE conditions (OData syntax: field eq value, field gt value, etc.)
- Use $select with valid field names only — do not guess or invent field names
- Use $orderby for sorting (use a space before asc/desc, e.g. "TotalAmount desc")
- The API is read-only.
"""


def make_project_client(config: Config) -> AIProjectClient:
    credential = ClientSecretCredential(
        tenant_id=config.azure_tenant_id,
        client_id=config.azure_sp_client_id,
        client_secret=config.azure_sp_client_secret,
    )
    return AIProjectClient(
        endpoint=config.foundry_project_endpoint,
        credential=credential,
    )


def make_openai_client(project: AIProjectClient) -> openai.OpenAI:
    return project.get_openai_client()
