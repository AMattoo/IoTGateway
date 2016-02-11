# Waher.Service.PcSensor

The **Waher.Service.PcSensor** project defines an application that converts your PC into an IoT sensor, by publishing performace counters as 
sensor values.

The first time the application is run, it provides a simple console interface for the user to provide network credentials. 
These credentials are then stored in the **xmpp.config** file. Passwords are hashed.

When it is read for the first time, it also creates a file called [categories.xml](#categories-xml) which lists all performance counter categories found, and 
if they should be included in the data readout or not. If new categories are found during the runtime of the application, the file is updated. 
By default, new categories are not included. 

## Console interface

The console interface can be used for two purposes:

1. To enter credentials for the XMPP connection. This is done the first time the application is run.
2. To view XMPP communication. This is done if a sniffer is enabled in the first step.

![Sniff](../../Images/Waher.Service.PcSensor.1.png)

## Sensor data

The application publishes performance counter values as sensordata using XMPP, and [XEP-0323](http://xmpp.org/extensions/xep-0323.html). 
Which performance counters to publish is defined in the [categories.xml](#categories-xml) file.

![Sniff](../../Images/Waher.Service.PcSensor.2.png)

## Chat interface

As the application is available through XMPP, it also publishes a chat interface:

![Sniff](../../Images/Waher.Service.PcSensor.3.png)

## Binary executable

You can test the application by downloading a [binary executable](../../Executables/Waher.Service.PcSensor.zip). If you don't have an XMPP client
you can use to chat with the sensor, or if the one you use does not support the XMPP IoT XEPs, you can also download the
[WPF client](../../Executables/Waher.Client.WPF.zip) available in the solution.

## Categories XML

After the first readout, a file called **categories.xml** is created. It includes all performance counter categories found. By default, no categories
are included in the readout. The file is updated if new categories are installed. To publish a category, set the **include** attribute to **true**.

In multi-instance categories, all instances are included by default, if not specified otherwise. To limit the category to certain instance names,
specify which ones using **Instance** elements.

### Example

Following is an example of a **categories.xml** file. Ellipsis has been used to shorten the example.

```XML
<?xml version="1.0" encoding="utf-8"?>
<Categories xmlns="http://waher.se/PerformanceCounterCategories.xsd">
	<Category name=".NET CLR-data" include="true" />
	<Category name=".NET CLR-n�tverk" include="true" />
	<Category name=".NET CLR-n�tverk 4.0.0.0" include="true" />
	<Category name=".NET-dataprovider f�r Oracle" include="false" />
	<Category name=".NET-dataprovider f�r SqlServer" include="false" />
	...
	<Category name="Process" include="false" />
	<Category name="Processor" include="true">
		<Instance name="_Total"/>
	</Category>
	<Category name="Processor f�r virtuell Hyper-V-v�xel" include="false" />
	...
	<Category name="XHCI Interrupter" include="false" />
	<Category name="XHCI TransferRing" include="false" />
</Categories>
```

## License

The source code provided in this project is provided open for the following uses:

* For **Personal evaluation**. Personal evaluation means evaluating the code, its libraries and underlying technologies, including learning 
	about underlying technologies.

* For **Academic use**. If you want to use the following code for academic use, all you need to do is to inform the author of who you are, what academic
	institution you work for (or study for), and in what projects you intend to use the code. All I ask in return is for an acknowledgement and
	visible attribution to this project.

* For **Security analysis**. If you perform any security analysis on the code, to see what security aspects the code might have,
	all I ask is that you inform me of any findings so that any vulnerabilities might be addressed. I am thankful for any such contributions,
	and will acknowledge them.

All rights to the source code are reserved. If you're interested in using the source code, as a whole, or partially, you need a license agreement
with the author. You can contact him through [LinkedIn](http://waher.se/).

This software is provided by the copyright holders and contributors "as is" and any express or implied warranties, including, but not limited to, 
the implied warranties of merchantability and fitness for a particular purpose are disclaimed. In no event shall the copyright owner or contributors 
be liable for any direct, indirect, incidental, special, exemplary, or consequential damages (including, but not limited to, procurement of substitute 
goods or services; loss of use, data, or profits; or business interruption) however caused and on any theory of liability, whether in contract, strict 
liability, or tort (including negligence or otherwise) arising in any way out of the use of this software, even if advised of the possibility of such 
damage.
