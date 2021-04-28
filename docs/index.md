# Instructions for manual run

1. download latest Windows release linked at the top
2. unzip the package and open a Cmdline window inside this folder
3. run xboxeraleaderboard.exe week31.csv week32.csv
4. check the cmdline output
5. copy the two tables in the cmdline output to XboxEra's Discourse editor
6. rejoice!

# Weekly stats

{% assign sorted-weekly-posts = site.tags.weekly | sort: 'post_date' %}

<ul>
  {% for post in sorted-weekly-posts %}
      <li>
        <a href="{{ post.url | relative_url }}">{{ post.title }}</a>
      </li>
  {% endfor %}
</ul>

# Monthly stats

...

