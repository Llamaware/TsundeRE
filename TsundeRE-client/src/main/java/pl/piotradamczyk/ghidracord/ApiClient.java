package pl.piotradamczyk.ghidracord;

import java.io.BufferedReader;
import java.io.FileInputStream;
import java.io.InputStreamReader;
import java.io.IOException;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.Properties;
import org.json.JSONArray;
import org.json.JSONObject;

public class ApiClient {
	
	private static String clientName;

    // This method reads settings from config.properties, calls the API,
    // and returns the users as a string array.
    public static String[] getUsers() {
        Properties config = new Properties();
        String currentDirectory = System.getProperty("user.dir");
        try (FileInputStream fis = new FileInputStream(currentDirectory + "\\tsundere.properties")) {
            config.load(fis);
        } catch (IOException e) {
            System.err.println("Error loading config file: " + e.getMessage());
            return new String[0];  // Return an empty array if config fails
        }

        String apiUrl = config.getProperty("api.url");
        String passphrase = config.getProperty("x.passphrase");
        clientName = config.getProperty("username");

        try {
            URL url = new URL(apiUrl); // TODO: update deprecated method
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("GET");
            // Set passphrase in the header
            conn.setRequestProperty("X-Passphrase", passphrase);

            int responseCode = conn.getResponseCode();
            if (responseCode == HttpURLConnection.HTTP_OK) {
                BufferedReader in = new BufferedReader(
                        new InputStreamReader(conn.getInputStream()));
                StringBuilder response = new StringBuilder();
                String inputLine;
                while ((inputLine = in.readLine()) != null) {
                    response.append(inputLine);
                }
                in.close();

                // Parse JSON response
                JSONObject jsonResponse = new JSONObject(response.toString());
                JSONArray usersJson = jsonResponse.getJSONArray("users");

                // Build the users array
                String[] users = new String[usersJson.length()];
                for (int i = 0; i < usersJson.length(); i++) {
                    users[i] = usersJson.getString(i);
                }
                return users;
            }
			System.out.println("Failed to connect: HTTP error code " + responseCode);
			return new String[0];
        } catch (Exception e) {
            e.printStackTrace();
            return new String[0];
        }
    }

    public static String getClientName() {
    	return clientName;
    }
}
